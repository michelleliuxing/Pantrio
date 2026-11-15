using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;

namespace Pantrio.Infrastructure.Data;

/// <summary>Every row of user data belongs to a household. Reads are filtered and writes are checked centrally.</summary>
public interface IHouseholdScoped
{
    Guid HouseholdId { get; set; }
}

public sealed class Household
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string TimeZone { get; set; } = "Australia/Sydney";
    public List<string> DietaryPrefs { get; set; } = [];
    public List<MealSlot> HiddenMealSlots { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSignInAt { get; set; } = DateTimeOffset.UtcNow;
    public List<UserLogin> Logins { get; set; } = [];
}

/// <summary>An external identity linked to a user. Adding a sign-in method later is a new provider value.</summary>
public sealed class UserLogin
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public required string Provider { get; set; }
    public required string Subject { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HouseholdMember
{
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public HouseholdRole Role { get; set; }
    public bool CanDelete { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public Household Household { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}

/// <summary>The household's ingredient catalogue. Recipe ingredients link here so the same ingredient merges across recipes.</summary>
public sealed class Ingredient : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public required string Name { get; set; }

    /// <summary>Lower-case, trimmed, single-spaced name. Unique per household.</summary>
    public required string NormalizedName { get; set; }

    public List<string> Aliases { get; set; } = [];
    public string? DefaultUnit { get; set; }
    public AisleCategory Category { get; set; } = AisleCategory.Other;
    public bool IsStaple { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static string Normalize(string name) =>
        string.Join(' ', name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

public enum MethodSource
{
    None,
    Custom,
    Ai,
}

public sealed class Recipe : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public required string Name { get; set; }
    public int Servings { get; set; } = 2;
    public List<string> Tags { get; set; } = [];
    public int? PrepMins { get; set; }
    public string? Notes { get; set; }
    public string? MethodText { get; set; }
    public MethodSource MethodSource { get; set; } = MethodSource.None;
    public string? SourceUrl { get; set; }
    public bool IsFavourite { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class RecipeIngredient : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    /// <summary>Null when the amount is vague; <see cref="QuantityText"/> then holds the phrase.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Normalised unit code (g, kg, ml, l, tsp, tbsp, cup, each, or a count label). Null when Quantity is null.</summary>
    public string? Unit { get; set; }

    public string? QuantityText { get; set; }
    public bool Optional { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>One per household per week (Monday start), created on first use.</summary>
public sealed class MealPlan : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<MealPlanEntry> Entries { get; set; } = [];
}

public sealed class MealPlanEntry : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public Guid MealPlanId { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    /// <summary>Null while the dish sits in the meal cart.</summary>
    public DateOnly? Day { get; set; }

    /// <summary>Null while the dish sits in the meal cart.</summary>
    public MealSlot? MealSlot { get; set; }

    public int? ServingsOverride { get; set; }
    public int SortOrder { get; set; }
    public Guid AddedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One live list per household. Totals are derived from <see cref="Sources"/>, never stored.</summary>
public sealed class ShoppingListItem : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }

    /// <summary>Null for extras.</summary>
    public Guid? IngredientId { get; set; }

    public required string Name { get; set; }

    /// <summary>Null for extras.</summary>
    public Dimension? Dimension { get; set; }

    /// <summary>"g", "ml", a count label, or "" for text. Null for extras.</summary>
    public string? UnitLabel { get; set; }

    public AisleCategory Category { get; set; } = AisleCategory.Other;
    public ItemState State { get; set; } = ItemState.Needed;
    public bool IsExtra { get; set; }

    /// <summary>Free text quantity for extras, e.g. "2 L".</summary>
    public string? ExtraQuantityText { get; set; }

    public bool InCheckGroup { get; set; }
    public decimal? BoughtTotal { get; set; }
    public bool AmountIncreased { get; set; }
    public Guid? AddedBy { get; set; }
    public Guid? CheckedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ListItemSourceRow> Sources { get; set; } = [];
}

/// <summary>Provenance: one row per plan entry per list item, quantity in base units.</summary>
public sealed class ListItemSourceRow : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public Guid ListItemId { get; set; }
    public Guid MealPlanId { get; set; }

    /// <summary>Not a foreign key: a removed entry's share stays until the next rebuild.</summary>
    public Guid MealPlanEntryId { get; set; }

    /// <summary>Recipe name at build time, so the source still reads well after the entry is gone.</summary>
    public string RecipeName { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }
    public string? QuantityText { get; set; }
    public bool Cleared { get; set; }
}

/// <summary>One row per AI call: makes cost visible and backs the daily caps.</summary>
public sealed class AiUsage : IHouseholdScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HouseholdId { get; set; }
    public Guid UserId { get; set; }
    public required string Purpose { get; set; }
    public required string Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool Succeeded { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
