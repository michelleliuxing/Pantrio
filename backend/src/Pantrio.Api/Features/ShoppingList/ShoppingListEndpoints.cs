using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Features.MealPlans;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.ShoppingList;

public sealed record ListSourceDto(Guid EntryId, string RecipeName, string Amount, DateOnly WeekStart);

public sealed record ShoppingListItemDto(
    Guid Id,
    string Name,
    AisleCategory Category,
    ItemState State,
    bool IsExtra,
    bool InCheckGroup,
    Dimension? Dimension,
    string? UnitLabel,
    /// <summary>Exact base-unit total of active sources. Null for text lines and extras.</summary>
    decimal? Total,
    /// <summary>Display text: "800 g", "3", "5 cloves", "to taste", or an extra's free-text quantity.</summary>
    string Amount,
    bool AmountIncreased,
    /// <summary>What was ticked as bought before the amount grew, e.g. "400 g".</summary>
    string? BoughtAmount,
    IReadOnlyList<ListSourceDto> Sources,
    /// <summary>The catalogue entry behind the line (null for extras).</summary>
    Guid? IngredientId,
    /// <summary>Where to buy it, read live from the catalogue (extras match by name).</summary>
    string? PurchaseUrl);

public sealed record ShoppingListDto(IReadOnlyList<ShoppingListItemDto> Items);

public sealed record BuildListRequest([property: Required] DateOnly WeekStart);

public sealed record AddRecipeToListRequest([property: Required] Guid RecipeId, [property: Range(1, 50)] int? Servings);

public sealed record AddExtraRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(60)] string? QuantityText,
    AisleCategory? Category);

public sealed record UpdateListItemRequest(
    ItemState? State,
    bool? InCheckGroup,
    [property: StringLength(200, MinimumLength = 1)] string? Name,
    [property: StringLength(60)] string? QuantityText,
    AisleCategory? Category);

public static class ShoppingListEndpoints
{
    public static RouteGroupBuilder MapShoppingListEndpoints(this RouteGroupBuilder api)
    {
        var list = api.MapGroup("/shopping-list").WithTags("Shopping list").RequireMember();

        list.MapGet("/", async (ShoppingListService service, PantrioDbContext db, CancellationToken ct) =>
                TypedResults.Ok(await ToDtoAsync(db, await service.LoadRowsAsync(ct), ct)))
            .WithName("GetShoppingList");

        list.MapPost("/build", async Task<Results<Ok<ShoppingListDto>, ValidationProblem>> (
                BuildListRequest body, ShoppingListService service, PantrioDbContext db, CancellationToken ct) =>
            {
                if (MealPlanEndpoints.WeekProblem(body.WeekStart) is { } problem) return problem;
                await service.BuildAsync(body.WeekStart, ct);
                return TypedResults.Ok(await ToDtoAsync(db, await service.LoadRowsAsync(ct), ct));
            })
            .WithName("BuildShoppingList")
            .RequireMember(HouseholdRole.Editor);

        list.MapPost("/add-recipe", async Task<Results<Ok<ShoppingListDto>, NotFound>> (
                AddRecipeToListRequest body, ShoppingListService service, PantrioDbContext db, CancellationToken ct) =>
            {
                if (!await db.Recipes.AnyAsync(r => r.Id == body.RecipeId, ct)) return TypedResults.NotFound();
                await service.AddRecipeAsync(body.RecipeId, body.Servings, await CurrentWeekAsync(db, ct), ct);
                return TypedResults.Ok(await ToDtoAsync(db, await service.LoadRowsAsync(ct), ct));
            })
            .WithName("AddRecipeToShoppingList")
            .RequireMember(HouseholdRole.Editor);

        list.MapPost("/items", async Task<Created<ShoppingListItemDto>> (AddExtraRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var category = body.Category;
                if (category is null)
                {
                    // Reuse the catalogue's aisle when the extra is a known ingredient ("milk" goes to dairy).
                    var normalized = Ingredient.Normalize(body.Name);
                    category = await db.Ingredients.Where(i => i.NormalizedName == normalized || i.Aliases.Contains(normalized))
                        .Select(i => (AisleCategory?)i.Category).FirstOrDefaultAsync(ct);
                }

                var row = new ShoppingListItem
                {
                    Name = body.Name.Trim(),
                    IsExtra = true,
                    ExtraQuantityText = string.IsNullOrWhiteSpace(body.QuantityText) ? null : body.QuantityText.Trim(),
                    Category = category ?? AisleCategory.Other,
                    AddedBy = db.Household.RequiredUserId,
                };
                db.ShoppingListItems.Add(row);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/shopping-list/items/{row.Id}", ToDto(row, new Dictionary<Guid, DateOnly>(), await LinksAsync(db, [row], ct)));
            })
            .WithName("AddShoppingListExtra")
            .RequireMember(HouseholdRole.Editor);

        list.MapPatch("/items/{id:guid}", async Task<Results<Ok<ShoppingListItemDto>, NotFound, ValidationProblem>> (
                Guid id, UpdateListItemRequest body, ShoppingListService service, PantrioDbContext db, CancellationToken ct) =>
            {
                var row = await db.ShoppingListItems.Include(i => i.Sources).FirstOrDefaultAsync(i => i.Id == id, ct);
                if (row is null) return TypedResults.NotFound();

                if (!row.IsExtra && (body.Name is not null || body.QuantityText is not null))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["name"] = ["Lines built from recipes change when the recipes or the plan change."],
                    });
                }

                if (body.State is { } state)
                {
                    service.ApplyUpdate(row, ShoppingListRules.SetState(ShoppingListService.ToDomain(row), state));
                    row.CheckedBy = state == ItemState.Needed ? null : db.Household.RequiredUserId;
                }

                if (body.InCheckGroup is { } inCheck)
                {
                    service.ApplyUpdate(row, ShoppingListRules.SetInCheckGroup(ShoppingListService.ToDomain(row), inCheck));
                }

                if (row.IsExtra)
                {
                    if (body.Name is { } name) row.Name = name.Trim();
                    if (body.QuantityText is { } qty) row.ExtraQuantityText = string.IsNullOrWhiteSpace(qty) ? null : qty.Trim();
                }

                if (body.Category is { } category) row.Category = category;
                row.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(row, await WeeksAsync(db, [row], ct), await LinksAsync(db, [row], ct)));
            })
            .WithName("UpdateShoppingListItem")
            .RequireMember(HouseholdRole.Editor);

        list.MapDelete("/items/{id:guid}", async Task<Results<NoContent, NotFound, ProblemHttpResult>> (Guid id, PantrioDbContext db, CancellationToken ct) =>
            {
                var row = await db.ShoppingListItems.FirstOrDefaultAsync(i => i.Id == id, ct);
                if (row is null) return TypedResults.NotFound();
                if (!row.IsExtra)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Can't remove a recipe line",
                        detail: "Mark it “Have it” instead, or remove the dish from the plan and rebuild.");
                }

                db.ShoppingListItems.Remove(row);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteShoppingListItem")
            .RequireMember(HouseholdRole.Editor);

        list.MapPost("/clear-bought", async (ShoppingListService service, PantrioDbContext db, CancellationToken ct) =>
            {
                await service.ClearCompletedAsync(ct);
                return TypedResults.Ok(await ToDtoAsync(db, await service.LoadRowsAsync(ct), ct));
            })
            .WithName("ClearCompletedItems")
            .RequireMember(HouseholdRole.Editor);

        return api;
    }

    /// <summary>Monday of the current week in the household's time zone.</summary>
    public static async Task<DateOnly> CurrentWeekAsync(PantrioDbContext db, CancellationToken ct)
    {
        var tzId = await db.Households.Where(h => h.Id == db.Household.RequiredHouseholdId).Select(h => h.TimeZone).SingleAsync(ct);
        var tz = TimeZoneInfo.TryFindSystemTimeZoneById(tzId, out var found) ? found : TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }

    private static async Task<Dictionary<Guid, DateOnly>> WeeksAsync(PantrioDbContext db, IEnumerable<ShoppingListItem> rows, CancellationToken ct)
    {
        var planIds = rows.SelectMany(r => r.Sources).Select(s => s.MealPlanId).Distinct().ToList();
        return await db.MealPlans.Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.WeekStart, ct);
    }

    private static async Task<ShoppingListDto> ToDtoAsync(PantrioDbContext db, List<ShoppingListItem> rows, CancellationToken ct)
    {
        var visibleRows = rows.Where(r => ShoppingListService.ToDomain(r).IsVisible).ToList();
        var weeks = await WeeksAsync(db, visibleRows, ct);
        var links = await LinksAsync(db, visibleRows, ct);
        var visible = visibleRows
            .OrderBy(r => r.Category).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => ToDto(r, weeks, links))
            .ToList();
        return new ShoppingListDto(visible);
    }

    /// <summary>Buy links for the given lines: by catalogue id for recipe lines, by catalogue name or alias for extras.</summary>
    private static async Task<Func<ShoppingListItem, string?>> LinksAsync(PantrioDbContext db, IReadOnlyCollection<ShoppingListItem> rows, CancellationToken ct)
    {
        var ids = rows.Where(r => r.IngredientId is not null).Select(r => r.IngredientId!.Value).Distinct().ToList();
        var names = rows.Where(r => r.IsExtra).Select(r => Ingredient.Normalize(r.Name)).Distinct().ToList();
        if (ids.Count == 0 && names.Count == 0)
        {
            return _ => null;
        }

        var linked = await db.Ingredients.AsNoTracking()
            .Where(i => i.PurchaseUrl != null
                        && (ids.Contains(i.Id) || names.Contains(i.NormalizedName) || i.Aliases.Any(a => names.Contains(a))))
            .Select(i => new { i.Id, i.NormalizedName, i.Aliases, i.PurchaseUrl })
            .ToListAsync(ct);
        var byId = linked.ToDictionary(i => i.Id, i => i.PurchaseUrl);
        var byName = new Dictionary<string, string?>();
        foreach (var i in linked)
        {
            byName.TryAdd(i.NormalizedName, i.PurchaseUrl);
            foreach (var alias in i.Aliases) byName.TryAdd(alias, i.PurchaseUrl);
        }

        return row => row.IngredientId is { } id
            ? byId.GetValueOrDefault(id)
            : row.IsExtra ? byName.GetValueOrDefault(Ingredient.Normalize(row.Name)) : null;
    }

    private static ShoppingListItemDto ToDto(ShoppingListItem row, IReadOnlyDictionary<Guid, DateOnly> weeks, Func<ShoppingListItem, string?> link)
    {
        var item = ShoppingListService.ToDomain(row);
        var amount = row.IsExtra ? row.ExtraQuantityText ?? string.Empty : ShoppingListRules.FormatTotal(item);
        string? bought = row.AmountIncreased && row.BoughtTotal is { } b && row.Dimension is { } d
            ? QuantityFormatter.Format(d, b, row.UnitLabel ?? string.Empty)
            : null;

        var sources = row.Sources.Where(s => !s.Cleared)
            .OrderBy(s => weeks.GetValueOrDefault(s.MealPlanId)).ThenBy(s => s.RecipeName)
            .Select(s => new ListSourceDto(s.MealPlanEntryId, s.RecipeName, ShoppingListService.FormatShare(row, s), weeks.GetValueOrDefault(s.MealPlanId)))
            .ToList();

        return new ShoppingListItemDto(row.Id, row.Name, row.Category, row.State, row.IsExtra, row.InCheckGroup, row.Dimension,
            row.UnitLabel, item.ActiveTotal, amount, row.AmountIncreased, bought, sources, row.IngredientId, link(row));
    }
}
