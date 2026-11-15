using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Recipes;

/// <summary>Saves recipes. The ingredient list is replaced as a whole; each row links to (or creates) a catalogue entry.</summary>
public sealed class RecipeService(PantrioDbContext db, IngredientMatcher matcher)
{
    public async Task<Recipe?> LoadAsync(Guid id, CancellationToken ct, bool track = false)
    {
        var query = db.Recipes.Include(r => r.Ingredients).ThenInclude(i => i.Ingredient).AsSplitQuery();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>When the recipe's photo last changed, without loading the image bytes.</summary>
    public Task<DateTimeOffset?> ImageVersionAsync(Guid recipeId, CancellationToken ct) =>
        db.RecipeImages.Where(i => i.RecipeId == recipeId).Select(i => (DateTimeOffset?)i.UpdatedAt).FirstOrDefaultAsync(ct);

    /// <summary>Validates the request beyond attributes. Returns field errors keyed like "ingredients[2].unit".</summary>
    public static Dictionary<string, string[]> Validate(RecipeUpsertRequest body)
    {
        var errors = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(body.SourceUrl) && !Urls.IsHttp(body.SourceUrl))
        {
            errors["sourceUrl"] = ["Use a full http(s) link."];
        }

        for (var i = 0; i < body.Ingredients.Count; i++)
        {
            var row = body.Ingredients[i];
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors[$"ingredients[{i}].name"] = ["Name is required."];
            }

            if (row.Quantity is not null && !Units.TryNormalise(row.Unit, out _))
            {
                errors[$"ingredients[{i}].unit"] = [$"'{row.Unit}' is not a unit."];
            }

            if (!string.IsNullOrWhiteSpace(row.PurchaseUrl) && !Urls.IsHttp(row.PurchaseUrl))
            {
                errors[$"ingredients[{i}].purchaseUrl"] = ["Use a full http(s) link."];
            }
        }

        return errors;
    }

    public async Task ApplyAsync(Recipe recipe, RecipeUpsertRequest body, CancellationToken ct)
    {
        recipe.Name = body.Name.Trim();
        recipe.Servings = body.Servings;
        recipe.Tags = CleanTags(body.Tags);
        recipe.PrepMins = body.PrepMins;
        recipe.Notes = Blank(body.Notes);
        recipe.MethodText = Blank(body.MethodText);
        recipe.MethodSource = recipe.MethodText is null ? MethodSource.None
            : recipe.MethodSource == MethodSource.None ? MethodSource.Custom : recipe.MethodSource;
        recipe.SourceUrl = Blank(body.SourceUrl);
        recipe.IsFavourite = body.IsFavourite ?? recipe.IsFavourite;
        recipe.UpdatedAt = DateTimeOffset.UtcNow;

        var catalogue = await ResolveIngredientsAsync(body.Ingredients, ct);

        db.RecipeIngredients.RemoveRange(recipe.Ingredients);
        recipe.Ingredients.Clear();
        for (var i = 0; i < body.Ingredients.Count; i++)
        {
            var row = body.Ingredients[i];
            var ingredient = catalogue[i];
            var hasQuantity = row.Quantity is not null;
            // Entities get their UUIDv7 key in code, so new children must be added explicitly:
            // attached through an already-tracked parent, EF would take them for existing rows.
            var line = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                IngredientId = ingredient.Id,
                Ingredient = ingredient,
                Quantity = row.Quantity,
                Unit = hasQuantity ? Units.Parse(row.Unit).Code : null,
                QuantityText = Blank(row.QuantityText),
                Optional = row.Optional,
                Note = Blank(row.Note),
                SortOrder = i,
            };
            db.RecipeIngredients.Add(line); // relationship fix-up also puts it into recipe.Ingredients
        }
    }

    /// <summary>
    /// For each row: the catalogue entry it names by id (only if it is this household's), else an exact or alias
    /// match on its name, else a new entry. Category and staple edits made in the recipe editor update the catalogue.
    /// </summary>
    private async Task<List<Ingredient>> ResolveIngredientsAsync(IReadOnlyList<RecipeIngredientInput> rows, CancellationToken ct)
    {
        var ids = rows.Where(r => r.IngredientId is not null).Select(r => r.IngredientId!.Value).Distinct().ToList();
        var byId = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        var matches = await matcher.MatchAsync(rows.Select(r => r.Name), ct);
        var created = new Dictionary<string, Ingredient>();
        var result = new List<Ingredient>(rows.Count);

        foreach (var row in rows)
        {
            var normalized = Ingredient.Normalize(row.Name);
            Ingredient? ingredient = null;
            if (row.IngredientId is { } id && byId.TryGetValue(id, out var picked))
            {
                ingredient = picked;
            }
            else if (matches.TryGetValue(normalized, out var match) && match.Kind != MatchKind.Fuzzy)
            {
                ingredient = match.Ingredient;
            }
            else if (!created.TryGetValue(normalized, out ingredient))
            {
                ingredient = new Ingredient
                {
                    Name = row.Name.Trim(),
                    NormalizedName = normalized,
                    Category = row.Category ?? AisleCategory.Other,
                    IsStaple = row.IsStaple ?? false,
                    DefaultUnit = row.Quantity is not null && Units.TryNormalise(row.Unit, out var u) ? u.Code : null,
                    PurchaseUrl = row.PurchaseUrl is null ? null : Urls.Normalize(row.PurchaseUrl),
                };
                db.Ingredients.Add(ingredient);
                created[normalized] = ingredient;
            }

            if (row.Category is { } category && ingredient.Category != category) ingredient.Category = category;
            if (row.IsStaple is { } staple && ingredient.IsStaple != staple) ingredient.IsStaple = staple;
            if (row.PurchaseUrl is { } url) ingredient.PurchaseUrl = Urls.Normalize(url);
            result.Add(ingredient);
        }

        return result;
    }

    public static List<string> CleanTags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length is > 0 and <= 40).Distinct().Take(20).ToList();

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static ValidationProblem Problem(Dictionary<string, string[]> errors) => TypedResults.ValidationProblem(errors);
}
