using Pantrio.Domain.Quantities;

namespace Pantrio.Domain.ShoppingList;

public enum ItemState
{
    Needed,
    Bought,
    HaveIt,
}

/// <summary>Recipe-derived list lines merge on catalogue ingredient + dimension + base unit label.</summary>
public sealed record ItemKey(Guid IngredientId, Dimension Dimension, string UnitLabel);

/// <summary>
/// One plan entry's share of one recipe ingredient, already scaled. Optional ingredients never become contributions.
/// </summary>
public sealed record Contribution(
    Guid MealPlanId,
    Guid EntryId,
    Guid IngredientId,
    string IngredientName,
    AisleCategory Category,
    bool IsStaple,
    Amount Amount)
{
    public ItemKey Key => new(IngredientId, Amount.Dimension, Amount.UnitLabel);
}

/// <summary>In-memory shopping list line. Mirrors the persisted row so the same rules run in tests and in the API.</summary>
public sealed class ListItem
{
    public required Guid Id { get; init; }

    /// <summary>Null for extras, which have no catalogue ingredient.</summary>
    public ItemKey? Key { get; init; }

    public required string Name { get; set; }

    public AisleCategory Category { get; set; } = AisleCategory.Other;

    public ItemState State { get; set; } = ItemState.Needed;

    public bool IsExtra { get; init; }

    /// <summary>Staples sit in "Check you have" until the person moves them to needed.</summary>
    public bool InCheckGroup { get; set; }

    /// <summary>Active total (base units) at the moment the item was ticked bought.</summary>
    public decimal? BoughtTotal { get; set; }

    /// <summary>Set when a rebuild returned a bought item to needed because its amount grew.</summary>
    public bool AmountIncreased { get; set; }

    public List<ListItemSource> Sources { get; init; } = [];

    public IEnumerable<ListItemSource> ActiveSources => Sources.Where(s => !s.Cleared);

    /// <summary>Derived, never stored: the sum of non-cleared sources. Null for text and extras.</summary>
    public decimal? ActiveTotal =>
        Key is null || Key.Dimension == Dimension.Text ? null : ActiveSources.Sum(s => s.Quantity ?? 0m);

    /// <summary>Distinct phrases from active text sources, in first-seen order.</summary>
    public IReadOnlyList<string> ActiveTexts =>
        ActiveSources.SelectMany(s => SplitTexts(s.QuantityText)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Extras always show; recipe-derived items show while they have an active source.</summary>
    public bool IsVisible => IsExtra || ActiveSources.Any();

    internal static IEnumerable<string> SplitTexts(string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : text.Split(TextSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal const string TextSeparator = "; ";
}

/// <summary>Provenance: one row per plan entry contributing to an item. Quantity is in base units.</summary>
public sealed class ListItemSource
{
    public required Guid Id { get; init; }

    public required Guid MealPlanId { get; init; }

    /// <summary>The plan entry. Not a foreign key: a removed entry's source survives until the next rebuild.</summary>
    public required Guid EntryId { get; init; }

    public decimal? Quantity { get; set; }

    public string? QuantityText { get; set; }

    /// <summary>Set by "clear completed": the share was bought and dealt with, so it no longer counts.</summary>
    public bool Cleared { get; set; }
}
