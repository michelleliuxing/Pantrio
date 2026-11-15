namespace Pantrio.Domain.ShoppingList;

/// <summary>
/// The minimal set of writes that takes a list from its current state to the planned one.
/// Infrastructure applies it to the database in one transaction; <see cref="ListChangeSetExtensions.ApplyTo"/>
/// applies it to the in-memory model (used by tests and to derive item-level changes).
/// </summary>
public sealed class ListChangeSet
{
    public List<ListItem> ItemsToCreate { get; } = [];

    /// <summary>New sources and the item each belongs to (an existing item or one in <see cref="ItemsToCreate"/>).</summary>
    public List<(Guid ItemId, ListItemSource Source)> SourcesToAdd { get; } = [];

    public List<SourceUpdate> SourcesToUpdate { get; } = [];

    public List<Guid> SourcesToDelete { get; } = [];

    public List<ItemUpdate> ItemsToUpdate { get; } = [];

    public List<Guid> ItemsToDelete { get; } = [];

    public bool IsEmpty =>
        ItemsToCreate.Count == 0 && SourcesToAdd.Count == 0 && SourcesToUpdate.Count == 0
        && SourcesToDelete.Count == 0 && ItemsToUpdate.Count == 0 && ItemsToDelete.Count == 0;
}

public sealed record SourceUpdate(Guid SourceId, decimal? Quantity, string? QuantityText, bool Cleared);

/// <summary>Full replacement of an item's mutable fields.</summary>
public sealed record ItemUpdate(
    Guid ItemId,
    string Name,
    AisleCategory Category,
    ItemState State,
    bool InCheckGroup,
    decimal? BoughtTotal,
    bool AmountIncreased)
{
    public static ItemUpdate From(ListItem item) =>
        new(item.Id, item.Name, item.Category, item.State, item.InCheckGroup, item.BoughtTotal, item.AmountIncreased);
}

public static class ListChangeSetExtensions
{
    /// <summary>Returns a new item list with the change set applied. The input is not modified.</summary>
    public static List<ListItem> ApplyTo(this ListChangeSet changes, IEnumerable<ListItem> items)
    {
        var result = items.Select(Clone).ToList();
        result.AddRange(changes.ItemsToCreate.Select(Clone));
        var byId = result.ToDictionary(i => i.Id);

        foreach (var (itemId, source) in changes.SourcesToAdd)
        {
            byId[itemId].Sources.Add(Clone(source));
        }

        var deleteSources = changes.SourcesToDelete.ToHashSet();
        var updates = changes.SourcesToUpdate.ToDictionary(u => u.SourceId);
        foreach (var item in result)
        {
            item.Sources.RemoveAll(s => deleteSources.Contains(s.Id));
            foreach (var source in item.Sources)
            {
                if (updates.TryGetValue(source.Id, out var u))
                {
                    source.Quantity = u.Quantity;
                    source.QuantityText = u.QuantityText;
                    source.Cleared = u.Cleared;
                }
            }
        }

        foreach (var u in changes.ItemsToUpdate)
        {
            var item = byId[u.ItemId];
            item.Name = u.Name;
            item.Category = u.Category;
            item.State = u.State;
            item.InCheckGroup = u.InCheckGroup;
            item.BoughtTotal = u.BoughtTotal;
            item.AmountIncreased = u.AmountIncreased;
        }

        var deleteItems = changes.ItemsToDelete.ToHashSet();
        result.RemoveAll(i => deleteItems.Contains(i.Id));
        return result;
    }

    internal static ListItem Clone(ListItem item) => new()
    {
        Id = item.Id,
        Key = item.Key,
        Name = item.Name,
        Category = item.Category,
        State = item.State,
        IsExtra = item.IsExtra,
        InCheckGroup = item.InCheckGroup,
        BoughtTotal = item.BoughtTotal,
        AmountIncreased = item.AmountIncreased,
        Sources = item.Sources.Select(Clone).ToList(),
    };

    internal static ListItemSource Clone(ListItemSource s) => new()
    {
        Id = s.Id,
        MealPlanId = s.MealPlanId,
        EntryId = s.EntryId,
        Quantity = s.Quantity,
        QuantityText = s.QuantityText,
        Cleared = s.Cleared,
    };
}
