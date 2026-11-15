using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.MealPlans;

public sealed record MealPlanEntryDto(
    Guid Id,
    Guid RecipeId,
    string RecipeName,
    int RecipeServings,
    DateOnly? Day,
    MealSlot? MealSlot,
    int? ServingsOverride,
    int SortOrder);

/// <summary>A week's plan. <see cref="Id"/> is null until something is first added to the week.</summary>
public sealed record MealPlanDto(Guid? Id, DateOnly WeekStart, IReadOnlyList<MealPlanEntryDto> Entries);

public sealed record AddEntryRequest(
    [property: Required] Guid RecipeId,
    DateOnly? Day,
    MealSlot? MealSlot,
    [property: Range(1, 50)] int? ServingsOverride);

/// <summary>Full replacement of an entry's placement and servings. Day and slot both null means the meal cart.</summary>
public sealed record UpdateEntryRequest(
    DateOnly? Day,
    MealSlot? MealSlot,
    [property: Range(1, 50)] int? ServingsOverride);

public static class MealPlanEndpoints
{
    public static RouteGroupBuilder MapMealPlanEndpoints(this RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/meal-plans").WithTags("Meal plans").RequireMember();

        plans.MapGet("/{weekStart}", async Task<Results<Ok<MealPlanDto>, ValidationProblem>> (DateOnly weekStart, PantrioDbContext db, CancellationToken ct) =>
            {
                if (WeekProblem(weekStart) is { } problem) return problem;
                return TypedResults.Ok(await LoadAsync(db, weekStart, ct));
            })
            .WithName("GetMealPlan");

        plans.MapPost("/{weekStart}/entries", async Task<Results<Created<MealPlanEntryDto>, ValidationProblem, NotFound>> (
                DateOnly weekStart, AddEntryRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                if (WeekProblem(weekStart) is { } weekError) return weekError;
                if (PlacementProblem(weekStart, body.Day, body.MealSlot) is { } placementError) return placementError;

                var recipe = await db.Recipes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == body.RecipeId, ct);
                if (recipe is null) return TypedResults.NotFound();

                var plan = await GetOrCreateAsync(db, weekStart, ct);
                var nextOrder = await db.MealPlanEntries.Where(e => e.MealPlanId == plan.Id).Select(e => (int?)e.SortOrder).MaxAsync(ct) ?? -1;
                var entry = new MealPlanEntry
                {
                    MealPlanId = plan.Id,
                    RecipeId = recipe.Id,
                    Day = body.Day,
                    MealSlot = body.MealSlot,
                    ServingsOverride = body.ServingsOverride == recipe.Servings ? null : body.ServingsOverride,
                    SortOrder = nextOrder + 1,
                    AddedBy = db.Household.RequiredUserId,
                };
                db.MealPlanEntries.Add(entry);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/meal-plan-entries/{entry.Id}", ToDto(entry, recipe));
            })
            .WithName("AddMealPlanEntry")
            .RequireMember(HouseholdRole.Editor);

        plans.MapPost("/{weekStart}/copy-previous", async Task<Results<Ok<MealPlanDto>, ValidationProblem>> (
                DateOnly weekStart, PantrioDbContext db, CancellationToken ct) =>
            {
                if (WeekProblem(weekStart) is { } problem) return problem;

                var previous = await db.MealPlans.AsNoTracking().Include(p => p.Entries)
                    .FirstOrDefaultAsync(p => p.WeekStart == weekStart.AddDays(-7), ct);
                if (previous is not null && previous.Entries.Count > 0)
                {
                    var plan = await GetOrCreateAsync(db, weekStart, ct);
                    var order = await db.MealPlanEntries.Where(e => e.MealPlanId == plan.Id).Select(e => (int?)e.SortOrder).MaxAsync(ct) ?? -1;
                    foreach (var e in previous.Entries.OrderBy(e => e.SortOrder))
                    {
                        db.MealPlanEntries.Add(new MealPlanEntry
                        {
                            MealPlanId = plan.Id,
                            RecipeId = e.RecipeId,
                            Day = e.Day?.AddDays(7),
                            MealSlot = e.MealSlot,
                            ServingsOverride = e.ServingsOverride,
                            SortOrder = ++order,
                            AddedBy = db.Household.RequiredUserId,
                        });
                    }

                    await db.SaveChangesAsync(ct);
                }

                return TypedResults.Ok(await LoadAsync(db, weekStart, ct));
            })
            .WithName("CopyPreviousWeek")
            .RequireMember(HouseholdRole.Editor);

        var entries = api.MapGroup("/meal-plan-entries").WithTags("Meal plans").RequireMember(HouseholdRole.Editor);

        entries.MapPut("/{id:guid}", async Task<Results<Ok<MealPlanEntryDto>, NotFound, ValidationProblem>> (
                Guid id, UpdateEntryRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var entry = await db.MealPlanEntries.Include(e => e.Recipe).FirstOrDefaultAsync(e => e.Id == id, ct);
                if (entry is null) return TypedResults.NotFound();

                var weekStart = await db.MealPlans.Where(p => p.Id == entry.MealPlanId).Select(p => p.WeekStart).SingleAsync(ct);
                if (PlacementProblem(weekStart, body.Day, body.MealSlot) is { } problem) return problem;

                entry.Day = body.Day;
                entry.MealSlot = body.MealSlot;
                entry.ServingsOverride = body.ServingsOverride == entry.Recipe.Servings ? null : body.ServingsOverride;
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(entry, entry.Recipe));
            })
            .WithName("UpdateMealPlanEntry");

        entries.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, PantrioDbContext db, CancellationToken ct) =>
            {
                var entry = await db.MealPlanEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
                if (entry is null) return TypedResults.NotFound();
                db.MealPlanEntries.Remove(entry);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteMealPlanEntry");

        return api;
    }

    internal static async Task<MealPlan> GetOrCreateAsync(PantrioDbContext db, DateOnly weekStart, CancellationToken ct)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(p => p.WeekStart == weekStart, ct);
        if (plan is not null)
        {
            return plan;
        }

        plan = new MealPlan { WeekStart = weekStart };
        db.MealPlans.Add(plan);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request created the week at the same moment; use that one.
            db.Entry(plan).State = EntityState.Detached;
            plan = await db.MealPlans.SingleAsync(p => p.WeekStart == weekStart, ct);
        }

        return plan;
    }

    private static async Task<MealPlanDto> LoadAsync(PantrioDbContext db, DateOnly weekStart, CancellationToken ct)
    {
        var plan = await db.MealPlans.AsNoTracking().FirstOrDefaultAsync(p => p.WeekStart == weekStart, ct);
        if (plan is null)
        {
            return new MealPlanDto(null, weekStart, []);
        }

        var entries = await db.MealPlanEntries.AsNoTracking()
            .Where(e => e.MealPlanId == plan.Id)
            .OrderBy(e => e.SortOrder)
            .Select(e => new MealPlanEntryDto(e.Id, e.RecipeId, e.Recipe.Name, e.Recipe.Servings, e.Day, e.MealSlot, e.ServingsOverride, e.SortOrder))
            .ToListAsync(ct);
        return new MealPlanDto(plan.Id, weekStart, entries);
    }

    private static MealPlanEntryDto ToDto(MealPlanEntry e, Recipe r) =>
        new(e.Id, r.Id, r.Name, r.Servings, e.Day, e.MealSlot, e.ServingsOverride, e.SortOrder);

    internal static ValidationProblem? WeekProblem(DateOnly weekStart) =>
        weekStart.DayOfWeek == DayOfWeek.Monday
            ? null
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["weekStart"] = ["Weeks start on a Monday."] });

    private static ValidationProblem? PlacementProblem(DateOnly weekStart, DateOnly? day, MealSlot? slot)
    {
        if (day is null != slot is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["day"] = ["Give both a day and a meal, or neither for the meal cart."] });
        }

        if (day is { } d && (d < weekStart || d > weekStart.AddDays(6)))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["day"] = ["That day is not in this week."] });
        }

        return null;
    }
}
