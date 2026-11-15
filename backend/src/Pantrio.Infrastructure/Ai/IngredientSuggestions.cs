namespace Pantrio.Infrastructure.Ai;

/// <summary>What the person asked for. Dish name and notes are user text and are passed to the model as data only.</summary>
public sealed record SuggestionRequest(
    string DishName,
    int Servings,
    string? Notes,
    IReadOnlyList<string> DietaryPrefs,
    IReadOnlyList<string> AlreadyListed,
    bool MoreOnly);

/// <summary>One drafted ingredient, already validated and normalised (unit codes, category wire names).</summary>
public sealed record SuggestedIngredient(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? QuantityText,
    string Category,
    bool Optional,
    bool IsStaple,
    string? Note);

public sealed record SuggestionResult(
    int Servings,
    IReadOnlyList<SuggestedIngredient> Ingredients,
    string Model,
    int InputTokens,
    int OutputTokens);

/// <summary>Drafts a recipe's ingredients. Swappable: a fake for tests and keyless local dev, OpenAI in production.</summary>
public interface IIngredientSuggester
{
    string Model { get; }

    Task<SuggestionResult> SuggestAsync(SuggestionRequest request, CancellationToken ct);
}

/// <summary>The model's output could not be turned into a valid draft, even after a retry.</summary>
public sealed class SuggestionFailedException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class AiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-6-luna";

    /// <summary>For reasoning models: none, low, medium, high. Empty to omit.</summary>
    public string ReasoningEffort { get; set; } = "low";

    public int TimeoutSeconds { get; set; } = 30;
    public int DailyLimitPerHousehold { get; set; } = 60;
    public int DailyLimitGlobal { get; set; } = 600;
}
