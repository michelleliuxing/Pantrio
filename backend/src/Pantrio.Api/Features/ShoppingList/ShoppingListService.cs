using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Features.MealPlans;
using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.ShoppingList;

/// <summary>
/// Loads the household's list into the domain model, runs the pure rules, and applies the resulting change set.
/// All list rules live in <see cref="ShoppingListRules"/>; this class only translates and persists.
/// </summary>
public sealed class ShoppingListService(PantrioDbContext db)
{
    public async Task<List<ShoppingListItem>> LoadRowsAsync(CancellationToken ct) =>
        await db.ShoppingListItems.Include(i => i.Sources).AsSplitQuery().ToListAsync(ct);

    /// <summary>Builds (or rebuilds) the list from one week's plan entries, assigned and in the cart.</summary>
    public async Task BuildAsync(DateOnly weekStart, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockHouseholdListAsync(ct);

        var plan = await db.MealPlans.AsNoTracking().FirstOrDefaultAsync(p => p.WeekStart == weekStart, ct);
        var entries = plan is null
            ? []
            : await db.MealPlanEntries.AsNoTracking()
                .Where(e => e.MealPlanId == plan.Id)
                .Include(e => e.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(i => i.Ingredient)
                .AsSplitQuery()
                .ToListAsync(ct);

        var planned = entries.Select(e => new PlannedEntry(e.MealPlanId, e.Id, e.ServingsOverride, new PlannedRecipe(
            e.RecipeId,
            e.Recipe.Servings,
            e.Recipe.Ingredients.OrderBy(i => i.SortOrder).Select(i => new PlannedIngredient(
                i.IngredientId, i.Ingredient.Name, i.Ingredient.Category, i.Ingredient.IsStaple,
                i.Quantity, i.Unit, i.QuantityText, i.Optional)).ToList())));

        var rows = await LoadRowsAsync(ct);
        var changes = ShoppingListRules.Rebuild(rows.Select(ToDomain).ToList(), plan?.Id ?? Guid.Empty, ContributionBuilder.Build(planned));
        var recipeNames = entries.ToDictionary(e => e.Id, e => e.Recipe.Name);
        Apply(rows, changes, recipeNames);

        // Keep provenance labels current for this week's surviving shares (a recipe may have been renamed).
        foreach (var source in rows.SelectMany(r => r.Sources).Where(s => plan is not null && s.MealPlanId == plan.Id))
        {
            if (recipeNames.TryGetValue(source.MealPlanEntryId, out var name) && source.RecipeName != name)
            {
                source.RecipeName = name;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Planning a single dish "straight to the list": a cart entry in the current week, then a rebuild of that week.</summary>
    public async Task AddRecipeAsync(Guid recipeId, int? servings, DateOnly weekStart, CancellationToken ct)
    {
        var recipe = await db.Recipes.AsNoTracking().SingleAsync(r => r.Id == recipeId, ct);
        var plan = await MealPlanEndpoints.GetOrCreateAsync(db, weekStart, ct);
        var order = await db.MealPlanEntries.Where(e => e.MealPlanId == plan.Id).Select(e => (int?)e.SortOrder).MaxAsync(ct) ?? -1;
        db.MealPlanEntries.Add(new MealPlanEntry
        {
            MealPlanId = plan.Id,
            RecipeId = recipe.Id,
            ServingsOverride = servings == recipe.Servings ? null : servings,
            SortOrder = order + 1,
            AddedBy = db.Household.RequiredUserId,
        });
        await db.SaveChangesAsync(ct);
        await BuildAsync(weekStart, ct);
    }

    public async Task ClearCompletedAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockHouseholdListAsync(ct);
        var rows = await LoadRowsAsync(ct);
        Apply(rows, ShoppingListRules.ClearCompleted(rows.Select(ToDomain).ToList()), new Dictionary<Guid, string>());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Concurrent builds for one household are serialised so the unique (ingredient, dimension, unit) line is never raced.</summary>
    private Task LockHouseholdListAsync(CancellationToken ct)
    {
        var key = BitConverter.ToInt64(db.Household.RequiredHouseholdId.ToByteArray(), 0);
        return db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }

    public static ListItem ToDomain(ShoppingListItem row) => new()
    {
        Id = row.Id,
        Key = row.IngredientId is { } ing && row.Dimension is { } dim ? new ItemKey(ing, dim, row.UnitLabel ?? string.Empty) : null,
        Name = row.Name,
        Category = row.Category,
        State = row.State,
        IsExtra = row.IsExtra,
        InCheckGroup = row.InCheckGroup,
        BoughtTotal = row.BoughtTotal,
        AmountIncreased = row.AmountIncreased,
        Sources = row.Sources.Select(s => new ListItemSource
        {
            Id = s.Id,
            MealPlanId = s.MealPlanId,
            EntryId = s.MealPlanEntryId,
            Quantity = s.Quantity,
            QuantityText = s.QuantityText,
            Cleared = s.Cleared,
        }).ToList(),
    };

    public void ApplyUpdate(ShoppingListItem row, ItemUpdate u)
    {
        row.Name = u.Name;
        row.Category = u.Category;
        row.State = u.State;
        row.InCheckGroup = u.InCheckGroup;
        row.BoughtTotal = u.BoughtTotal;
        row.AmountIncreased = u.AmountIncreased;
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void Apply(List<ShoppingListItem> rows, ListChangeSet changes, IReadOnlyDictionary<Guid, string> recipeNames)
    {
        var byId = rows.ToDictionary(r => r.Id);
        var userId = db.Household.RequiredUserId;

        foreach (var item in changes.ItemsToCreate)
        {
            var row = new ShoppingListItem
            {
                Id = item.Id,
                IngredientId = item.Key!.IngredientId,
                Dimension = item.Key.Dimension,
                UnitLabel = item.Key.UnitLabel,
                Name = item.Name,
                Category = item.Category,
                State = item.State,
                InCheckGroup = item.InCheckGroup,
                AddedBy = userId,
            };
            db.ShoppingListItems.Add(row);
            rows.Add(row);
            byId[row.Id] = row;
        }

        foreach (var (itemId, source) in changes.SourcesToAdd)
        {
            db.ListItemSources.Add(new ListItemSourceRow
            {
                Id = source.Id,
                ListItemId = itemId,
                MealPlanId = source.MealPlanId,
                MealPlanEntryId = source.EntryId,
                RecipeName = recipeNames.GetValueOrDefault(source.EntryId, string.Empty),
                Quantity = source.Quantity,
                QuantityText = source.QuantityText,
                Cleared = source.Cleared,
            });
        }

        var sources = rows.SelectMany(r => r.Sources).ToDictionary(s => s.Id);
        foreach (var u in changes.SourcesToUpdate)
        {
            var s = sources[u.SourceId];
            s.Quantity = u.Quantity;
            s.QuantityText = u.QuantityText;
            s.Cleared = u.Cleared;
        }

        foreach (var id in changes.SourcesToDelete)
        {
            db.ListItemSources.Remove(sources[id]);
        }

        foreach (var u in changes.ItemsToUpdate)
        {
            ApplyUpdate(byId[u.ItemId], u);
        }

        foreach (var id in changes.ItemsToDelete)
        {
            db.ShoppingListItems.Remove(byId[id]);
        }
    }

    /// <summary>Formats a single source's share for the "comes from" detail. Counts are shown exactly, not rounded up.</summary>
    public static string FormatShare(ShoppingListItem item, ListItemSourceRow source) =>
        item.Dimension is null or Dimension.Text || source.Quantity is null
            ? source.QuantityText ?? string.Empty
            : QuantityFormatter.Format(item.Dimension.Value, source.Quantity.Value, item.UnitLabel ?? string.Empty, roundUpEach: false);
}
