using Microsoft.EntityFrameworkCore;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Ingredients;

public enum MatchKind
{
    Exact,
    Alias,
    Fuzzy,
}

public sealed record IngredientMatch(Ingredient Ingredient, MatchKind Kind, double Score);

/// <summary>Matches free-text ingredient names to the household catalogue: exact name, then alias, then trigram similarity.</summary>
public sealed class IngredientMatcher(PantrioDbContext db)
{
    public const double FuzzyThreshold = 0.45;

    public async Task<IReadOnlyDictionary<string, IngredientMatch>> MatchAsync(IEnumerable<string> names, CancellationToken ct)
    {
        var wanted = names.Select(Ingredient.Normalize).Where(n => n.Length > 0).Distinct().ToList();
        var result = new Dictionary<string, IngredientMatch>();
        if (wanted.Count == 0)
        {
            return result;
        }

        var exact = await db.Ingredients.Where(i => wanted.Contains(i.NormalizedName)).ToListAsync(ct);
        foreach (var i in exact)
        {
            result[i.NormalizedName] = new IngredientMatch(i, MatchKind.Exact, 1);
        }

        var remaining = wanted.Where(n => !result.ContainsKey(n)).ToList();
        if (remaining.Count > 0)
        {
            var aliased = await db.Ingredients.Where(i => i.Aliases.Any(a => remaining.Contains(a))).ToListAsync(ct);
            foreach (var name in remaining)
            {
                var hit = aliased.FirstOrDefault(i => i.Aliases.Contains(name));
                if (hit is not null)
                {
                    result[name] = new IngredientMatch(hit, MatchKind.Alias, 1);
                }
            }
        }

        foreach (var name in wanted.Where(n => !result.ContainsKey(n)))
        {
            var fuzzy = await db.Ingredients
                .Select(i => new { Ingredient = i, Score = PgFunctions.Similarity(i.NormalizedName, name) })
                .Where(x => x.Score >= FuzzyThreshold)
                .OrderByDescending(x => x.Score)
                .FirstOrDefaultAsync(ct);
            if (fuzzy is not null)
            {
                result[name] = new IngredientMatch(fuzzy.Ingredient, MatchKind.Fuzzy, fuzzy.Score);
            }
        }

        return result;
    }
}
