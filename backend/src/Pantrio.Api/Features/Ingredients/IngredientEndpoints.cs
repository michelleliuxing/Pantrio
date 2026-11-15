using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Ingredients;

public sealed record IngredientDto(Guid Id, string Name, IReadOnlyList<string> Aliases, string? DefaultUnit, AisleCategory Category, bool IsStaple)
{
    public static IngredientDto From(Ingredient i) => new(i.Id, i.Name, i.Aliases, i.DefaultUnit, i.Category, i.IsStaple);
}

public sealed record CreateIngredientRequest(
    [property: Required, StringLength(120, MinimumLength = 1)] string Name,
    AisleCategory? Category,
    bool? IsStaple,
    [property: StringLength(24)] string? DefaultUnit,
    [property: MaxLength(20)] IReadOnlyList<string>? Aliases);

public sealed record UpdateIngredientRequest(
    [property: StringLength(120, MinimumLength = 1)] string? Name,
    AisleCategory? Category,
    bool? IsStaple,
    [property: StringLength(24)] string? DefaultUnit,
    [property: MaxLength(20)] IReadOnlyList<string>? Aliases);

public sealed record UnitInfo(string Code, Dimension Dimension);

public sealed record ReferenceDataResponse(IReadOnlyList<UnitInfo> Units, IReadOnlyList<AisleCategory> Categories);

public static class IngredientEndpoints
{
    public static RouteGroupBuilder MapIngredientEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/reference", () => TypedResults.Ok(new ReferenceDataResponse(
                [
                    new("g", Dimension.Mass), new("kg", Dimension.Mass),
                    new("ml", Dimension.Volume), new("l", Dimension.Volume), new("tsp", Dimension.Volume),
                    new("tbsp", Dimension.Volume), new("cup", Dimension.Volume),
                    .. Units.KnownCountLabels.Select(l => new UnitInfo(l, Dimension.Count)),
                ],
                Enum.GetValues<AisleCategory>())))
            .WithName("GetReferenceData")
            .WithTags("Ingredients");

        var group = api.MapGroup("/ingredients").WithTags("Ingredients").RequireMember();

        group.MapGet("/", async (string? search, int? limit, PantrioDbContext db, CancellationToken ct) =>
            {
                var take = Math.Clamp(limit ?? 20, 1, 200);
                var query = db.Ingredients.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var q = Ingredient.Normalize(search);
                    var pattern = $"%{EscapeLike(q)}%";
                    query = query
                        .Where(i => EF.Functions.ILike(i.NormalizedName, pattern) || i.Aliases.Any(a => EF.Functions.ILike(a, pattern))
                                    || PgFunctions.Similarity(i.NormalizedName, q) >= IngredientMatcher.FuzzyThreshold)
                        .OrderByDescending(i => i.NormalizedName == q)
                        .ThenByDescending(i => i.NormalizedName.StartsWith(q))
                        .ThenByDescending(i => PgFunctions.Similarity(i.NormalizedName, q))
                        .ThenBy(i => i.Name);
                }
                else
                {
                    query = query.OrderBy(i => i.Name);
                }

                var items = await query.Take(take).ToListAsync(ct);
                return TypedResults.Ok(items.Select(IngredientDto.From).ToList());
            })
            .WithName("ListIngredients");

        group.MapPost("/", async Task<Results<Created<IngredientDto>, Conflict<IngredientDto>, ValidationProblem>> (
                CreateIngredientRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var normalized = Ingredient.Normalize(body.Name);
                var existing = await db.Ingredients.FirstOrDefaultAsync(i => i.NormalizedName == normalized, ct);
                if (existing is not null)
                {
                    return TypedResults.Conflict(IngredientDto.From(existing));
                }

                if (!TryUnit(body.DefaultUnit, out var unit, out var problem))
                {
                    return problem;
                }

                var ingredient = new Ingredient
                {
                    Name = body.Name.Trim(),
                    NormalizedName = normalized,
                    Category = body.Category ?? AisleCategory.Other,
                    IsStaple = body.IsStaple ?? false,
                    DefaultUnit = unit,
                    Aliases = CleanAliases(body.Aliases, normalized),
                };
                db.Ingredients.Add(ingredient);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/ingredients/{ingredient.Id}", IngredientDto.From(ingredient));
            })
            .WithName("CreateIngredient")
            .RequireMember(HouseholdRole.Editor);

        group.MapPatch("/{id:guid}", async Task<Results<Ok<IngredientDto>, NotFound, Conflict<IngredientDto>, ValidationProblem>> (
                Guid id, UpdateIngredientRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct);
                if (ingredient is null)
                {
                    return TypedResults.NotFound();
                }

                if (body.Name is { } name)
                {
                    var normalized = Ingredient.Normalize(name);
                    var clash = await db.Ingredients.FirstOrDefaultAsync(i => i.NormalizedName == normalized && i.Id != id, ct);
                    if (clash is not null)
                    {
                        return TypedResults.Conflict(IngredientDto.From(clash));
                    }

                    ingredient.Name = name.Trim();
                    ingredient.NormalizedName = normalized;
                }

                if (body.DefaultUnit is not null)
                {
                    if (!TryUnit(body.DefaultUnit, out var unit, out var problem))
                    {
                        return problem;
                    }

                    ingredient.DefaultUnit = unit;
                }

                if (body.Category is { } category) ingredient.Category = category;
                if (body.IsStaple is { } staple) ingredient.IsStaple = staple;
                if (body.Aliases is { } aliases) ingredient.Aliases = CleanAliases(aliases, ingredient.NormalizedName);

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(IngredientDto.From(ingredient));
            })
            .WithName("UpdateIngredient")
            .RequireMember(HouseholdRole.Editor);

        return api;
    }

    internal static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private static List<string> CleanAliases(IEnumerable<string>? aliases, string normalizedName) =>
        (aliases ?? []).Select(Ingredient.Normalize).Where(a => a.Length is > 0 and <= 120 && a != normalizedName).Distinct().ToList();

    private static bool TryUnit(string? raw, out string? unit, out ValidationProblem problem)
    {
        problem = null!;
        unit = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Units.TryNormalise(raw, out var def))
        {
            unit = def.Code;
            return true;
        }

        problem = TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["defaultUnit"] = [$"'{raw}' is not a unit."] });
        return false;
    }
}
