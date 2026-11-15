using System.ComponentModel.DataAnnotations;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Recipes;

public sealed record RecipeIngredientInput(
    Guid? IngredientId,
    [property: Required, StringLength(120, MinimumLength = 1)] string Name,
    [property: Range(typeof(decimal), "0.0001", "100000")] decimal? Quantity,
    [property: StringLength(24)] string? Unit,
    [property: StringLength(100)] string? QuantityText,
    bool Optional,
    bool? IsStaple,
    AisleCategory? Category,
    [property: StringLength(200)] string? Note);

public sealed record RecipeUpsertRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: Range(1, 50)] int Servings,
    [property: MaxLength(20)] IReadOnlyList<string>? Tags,
    [property: Range(0, 1440)] int? PrepMins,
    [property: StringLength(4000)] string? Notes,
    [property: StringLength(20000)] string? MethodText,
    [property: StringLength(2048)] string? SourceUrl,
    bool? IsFavourite,
    [property: Required, MaxLength(100)] IReadOnlyList<RecipeIngredientInput> Ingredients);

public sealed record UpdateRecipeFlagsRequest(bool? IsFavourite);

public sealed record RecipeIngredientDto(
    Guid Id,
    Guid IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? QuantityText,
    bool Optional,
    bool IsStaple,
    AisleCategory Category,
    string? Note,
    int SortOrder);

public sealed record RecipeDto(
    Guid Id,
    string Name,
    int Servings,
    IReadOnlyList<string> Tags,
    int? PrepMins,
    string? Notes,
    string? MethodText,
    MethodSource MethodSource,
    string? SourceUrl,
    bool IsFavourite,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RecipeIngredientDto> Ingredients)
{
    public static RecipeDto From(Recipe r) => new(
        r.Id, r.Name, r.Servings, r.Tags, r.PrepMins, r.Notes, r.MethodText, r.MethodSource, r.SourceUrl,
        r.IsFavourite, r.CreatedAt, r.UpdatedAt,
        r.Ingredients.OrderBy(i => i.SortOrder).Select(i => new RecipeIngredientDto(
            i.Id, i.IngredientId, i.Ingredient.Name, i.Quantity, i.Unit, i.QuantityText, i.Optional,
            i.Ingredient.IsStaple, i.Ingredient.Category, i.Note, i.SortOrder)).ToList());
}

public sealed record RecipeSummaryDto(
    Guid Id,
    string Name,
    int Servings,
    IReadOnlyList<string> Tags,
    int? PrepMins,
    bool IsFavourite,
    int IngredientCount,
    DateTimeOffset UpdatedAt);

public sealed record TagCount(string Tag, int Count);

public enum RecipeSort
{
    Name,
    Recent,
}
