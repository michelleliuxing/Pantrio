namespace Pantrio.Infrastructure.Ai;

/// <summary>
/// Deterministic stand-in for the AI provider. Used by tests and by local development when no API key is set,
/// so the whole draft flow can be exercised for free.
/// </summary>
public sealed class FakeIngredientSuggester : IIngredientSuggester
{
    public string Model => "fake";

    public int Calls { get; private set; }

    public Task<SuggestionResult> SuggestAsync(SuggestionRequest request, CancellationToken ct)
    {
        Calls++;
        var factor = request.Servings / 2m;
        var name = request.DishName.ToLowerInvariant();
        var list = new List<SuggestedIngredient>();

        if (name.Contains("curry") || name.Contains("katsu"))
        {
            list.AddRange(
            [
                new("chicken thigh fillets", 400 * factor, "g", null, "meat_seafood", false, false, "or breast"),
                new("brown onion", 1 * factor, "each", null, "produce", false, false, null),
                new("carrot", 1 * factor, "each", null, "produce", false, false, null),
                new("potato", 300 * factor, "g", null, "produce", false, false, null),
                new("japanese curry roux", 100 * factor, "g", null, "international", false, false, null),
                new("jasmine rice", 150 * factor, "g", null, "pantry", false, false, null),
                new("panko breadcrumbs", 1 * factor, "cup", null, "pantry", false, false, null),
                new("eggs", 1 * factor, "each", null, "dairy_eggs", false, false, null),
                new("plain flour", 2 * factor, "tbsp", null, "pantry", false, false, null),
                new("vegetable oil", 250, "ml", null, "pantry", false, true, "for frying"),
                new("salt", null, null, "to taste", "spices", false, true, null),
                new("pickled ginger", 1, "tbsp", null, "international", true, false, "to serve"),
            ]);
        }
        else
        {
            list.AddRange(
            [
                new("brown onion", 1 * factor, "each", null, "produce", false, false, null),
                new("garlic", 2 * factor, "clove", null, "produce", false, false, null),
                new("olive oil", 2, "tbsp", null, "pantry", false, true, null),
                new("salt", null, null, "to taste", "spices", false, true, null),
                new("black pepper", null, null, "to taste", "spices", false, true, null),
                new("parsley", 1, "bunch", null, "produce", true, false, "to garnish"),
            ]);
        }

        if (request.Notes?.Contains("no coriander", StringComparison.OrdinalIgnoreCase) == true)
        {
            list.RemoveAll(i => i.Name == "coriander");
        }

        if (request.MoreOnly)
        {
            var have = request.AlreadyListed.Select(n => n.Trim().ToLowerInvariant()).ToHashSet();
            list =
            [
                .. list.Where(i => !have.Contains(i.Name)),
                new("spring onion", 1, "bunch", null, "produce", true, false, "to garnish"),
            ];
            list = list.Where(i => !have.Contains(i.Name)).Take(4).ToList();
        }

        return Task.FromResult(new SuggestionResult(request.Servings, list, Model, 0, 0));
    }
}
