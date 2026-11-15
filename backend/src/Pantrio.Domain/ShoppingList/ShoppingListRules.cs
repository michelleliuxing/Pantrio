using Pantrio.Domain.Quantities;

namespace Pantrio.Domain.ShoppingList;

/// <summary>
/// The shopping list rules (context doc section 8). Every method is pure: it reads the current items and
/// returns a <see cref="ListChangeSet"/>.
/// </summary>
public static class ShoppingListRules
{
    /// <summary>
    /// Builds or rebuilds the list for one week. This is a diff, not a wipe:
    /// sources for entries still in the week are updated, sources for removed entries are deleted,
    /// a recipe-derived item with no sources left is removed, extras are never touched,
    /// item state survives, and a bought item whose active amount grows goes back to needed.
    /// Sources from other weeks are left alone, so building a new week adds to what remains.
    /// Cleared sources (already bought and cleared) stay cleared.
    /// </summary>
    public static ListChangeSet Rebuild(
        IReadOnlyCollection<ListItem> items,
        Guid mealPlanId,
        IEnumerable<Contribution> contributions,
        Func<Guid>? newId = null)
    {
        newId ??= Guid.CreateVersion7;
        var changes = new ListChangeSet();

        var desired = contributions
            .Where(c => c.MealPlanId == mealPlanId)
            .GroupBy(c => (c.Key, c.EntryId))
            .Select(g => new DesiredSource(g.Key.Key, g.Key.EntryId, g.First(), Sum(g), JoinTexts(g)))
            .ToList();

        var itemsByKey = items.Where(i => !i.IsExtra && i.Key is not null).ToDictionary(i => i.Key!);
        var existing = items
            .Where(i => !i.IsExtra && i.Key is not null)
            .SelectMany(i => i.Sources.Where(s => s.MealPlanId == mealPlanId).Select(s => (Item: i, Source: s)))
            .ToDictionary(x => (x.Item.Key!, x.Source.EntryId));

        var touched = new HashSet<Guid>();
        var seen = new HashSet<Guid>();
        var namesToRefresh = new Dictionary<Guid, Contribution>();

        foreach (var d in desired)
        {
            if (existing.TryGetValue((d.Key, d.EntryId), out var hit))
            {
                seen.Add(hit.Source.Id);
                namesToRefresh[hit.Item.Id] = d.Template;
                if (hit.Source.Quantity != d.Quantity || hit.Source.QuantityText != d.Text)
                {
                    changes.SourcesToUpdate.Add(new SourceUpdate(hit.Source.Id, d.Quantity, d.Text, hit.Source.Cleared));
                    touched.Add(hit.Item.Id);
                }

                continue;
            }

            if (!itemsByKey.TryGetValue(d.Key, out var item))
            {
                item = new ListItem
                {
                    Id = newId(),
                    Key = d.Key,
                    Name = d.Template.IngredientName,
                    Category = d.Template.Category,
                    InCheckGroup = d.Template.IsStaple,
                    State = ItemState.Needed,
                };
                itemsByKey[d.Key] = item;
                changes.ItemsToCreate.Add(item);
            }
            else
            {
                namesToRefresh[item.Id] = d.Template;
            }

            changes.SourcesToAdd.Add((item.Id, new ListItemSource
            {
                Id = newId(),
                MealPlanId = mealPlanId,
                EntryId = d.EntryId,
                Quantity = d.Quantity,
                QuantityText = d.Text,
            }));
            touched.Add(item.Id);
        }

        foreach (var ((_, _), hit) in existing)
        {
            if (!seen.Contains(hit.Source.Id))
            {
                changes.SourcesToDelete.Add(hit.Source.Id);
                touched.Add(hit.Item.Id);
            }
        }

        // Derive item-level changes from the source-level result.
        var after = changes.ApplyTo(items).ToDictionary(i => i.Id);
        var created = changes.ItemsToCreate.Select(i => i.Id).ToHashSet();
        foreach (var original in items.Where(i => !i.IsExtra))
        {
            var next = after[original.Id];
            if (touched.Contains(original.Id) && next.Sources.Count == 0)
            {
                changes.ItemsToDelete.Add(original.Id);
                continue;
            }

            var updated = ListChangeSetExtensions.Clone(next);
            if (namesToRefresh.TryGetValue(original.Id, out var template))
            {
                updated.Name = template.IngredientName;
                updated.Category = template.Category;
            }

            if (touched.Contains(original.Id) && updated.State == ItemState.Bought
                && updated.ActiveTotal is { } total && total > (updated.BoughtTotal ?? 0m))
            {
                updated.State = ItemState.Needed;
                updated.AmountIncreased = true;
            }

            if (ItemUpdate.From(updated) != ItemUpdate.From(original))
            {
                changes.ItemsToUpdate.Add(ItemUpdate.From(updated));
            }
        }

        // Newly created items never need an update entry.
        changes.ItemsToUpdate.RemoveAll(u => created.Contains(u.ItemId));
        return changes;
    }

    /// <summary>
    /// Changes an item's state. Ticking bought records the current total so a later increase can be detected.
    /// Any explicit state change clears the "amount increased" marker.
    /// </summary>
    public static ItemUpdate SetState(ListItem item, ItemState state) => ItemUpdate.From(item) with
    {
        State = state,
        BoughtTotal = state == ItemState.Bought ? item.ActiveTotal : null,
        AmountIncreased = false,
    };

    /// <summary>Moves a staple out of "Check you have" into the to-buy groups (or back).</summary>
    public static ItemUpdate SetInCheckGroup(ListItem item, bool inCheckGroup) =>
        ItemUpdate.From(item) with { InCheckGroup = inCheckGroup };

    /// <summary>
    /// "Clear completed": bought and have-it lines leave the list. Recipe-derived items keep their rows with
    /// sources marked cleared, so rebuilding the same week does not bring them back, while a new week's
    /// entries add fresh sources and the item returns as needed with only the new amount. Completed extras are deleted.
    /// </summary>
    public static ListChangeSet ClearCompleted(IEnumerable<ListItem> items)
    {
        var changes = new ListChangeSet();
        foreach (var item in items.Where(i => i.State != ItemState.Needed && i.IsVisible))
        {
            if (item.IsExtra)
            {
                changes.ItemsToDelete.Add(item.Id);
                continue;
            }

            foreach (var source in item.ActiveSources)
            {
                changes.SourcesToUpdate.Add(new SourceUpdate(source.Id, source.Quantity, source.QuantityText, Cleared: true));
            }

            changes.ItemsToUpdate.Add(ItemUpdate.From(item) with
            {
                State = ItemState.Needed,
                BoughtTotal = null,
                AmountIncreased = false,
            });
        }

        return changes;
    }

    /// <summary>Display text for an item's total: "800 g", "3", "5 cloves", or "to taste" for text lines.</summary>
    public static string FormatTotal(ListItem item)
    {
        if (item.Key is null)
        {
            return string.Empty;
        }

        if (item.Key.Dimension == Dimension.Text)
        {
            return string.Join(", ", item.ActiveTexts);
        }

        return QuantityFormatter.Format(item.Key.Dimension, item.ActiveTotal ?? 0m, item.Key.UnitLabel);
    }

    private static decimal? Sum(IEnumerable<Contribution> group)
    {
        var values = group.Select(c => c.Amount.BaseValue).ToList();
        return values.All(v => v is null) ? null : values.Sum(v => v ?? 0m);
    }

    private static string? JoinTexts(IEnumerable<Contribution> group)
    {
        var texts = group.Select(c => c.Amount.Text).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return texts.Count == 0 ? null : string.Join(ListItem.TextSeparator, texts);
    }

    private sealed record DesiredSource(ItemKey Key, Guid EntryId, Contribution Template, decimal? Quantity, string? Text);
}
