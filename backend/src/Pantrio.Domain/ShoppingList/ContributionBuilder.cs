using Pantrio.Domain.Quantities;

namespace Pantrio.Domain.ShoppingList;

public sealed record PlannedIngredient(
    Guid IngredientId,
    string Name,
    AisleCategory Category,
    bool IsStaple,
    decimal? Quantity,
    string? Unit,
    string? QuantityText,
    bool Optional);

public sealed record PlannedRecipe(Guid RecipeId, int Servings, IReadOnlyList<PlannedIngredient> Ingredients);

/// <summary>A dish in a week, either on a day/slot or in the meal cart. Both feed the list the same way.</summary>
public sealed record PlannedEntry(Guid MealPlanId, Guid EntryId, int? ServingsOverride, PlannedRecipe Recipe);

public static class ContributionBuilder
{
    /// <summary>Scales each entry's recipe ingredients. Optional ingredients are left off the list.</summary>
    public static IReadOnlyList<Contribution> Build(IEnumerable<PlannedEntry> entries)
    {
        var result = new List<Contribution>();
        foreach (var entry in entries)
        {
            var factor = Scaling.ForServings(entry.Recipe.Servings, entry.ServingsOverride);
            foreach (var ing in entry.Recipe.Ingredients.Where(i => !i.Optional))
            {
                var amount = Amount.FromRecipe(ing.Quantity, ing.Unit, ing.QuantityText).Scale(factor);
                result.Add(new Contribution(entry.MealPlanId, entry.EntryId, ing.IngredientId, ing.Name, ing.Category, ing.IsStaple, amount));
            }
        }

        return result;
    }
}
