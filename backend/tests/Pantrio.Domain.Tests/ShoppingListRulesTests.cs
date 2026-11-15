using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;

namespace Pantrio.Domain.Tests;

/// <summary>A small in-memory household: catalogue, recipes, a list, and helpers to build weeks.</summary>
internal sealed class Kitchen
{
    public static readonly Guid Week1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Week2 = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    private readonly Dictionary<string, (Guid Id, AisleCategory Category, bool Staple)> _catalogue = new();

    public List<ListItem> Items { get; private set; } = [];

    public PlannedIngredient Ing(string name, decimal? qty, string? unit, string? text = null,
        AisleCategory category = AisleCategory.Other, bool staple = false, bool optional = false)
    {
        if (!_catalogue.TryGetValue(name, out var c))
        {
            c = (Guid.NewGuid(), category, staple);
            _catalogue[name] = c;
        }

        return new PlannedIngredient(c.Id, name, c.Category, c.Staple, qty, unit, text, optional);
    }

    public static PlannedRecipe Recipe(int servings, params PlannedIngredient[] ingredients) =>
        new(Guid.NewGuid(), servings, ingredients);

    public static PlannedEntry Entry(Guid week, PlannedRecipe recipe, int? servings = null, Guid? id = null) =>
        new(week, id ?? Guid.NewGuid(), servings, recipe);

    public ListChangeSet Build(Guid week, params PlannedEntry[] entries)
    {
        var changes = ShoppingListRules.Rebuild(Items, week, ContributionBuilder.Build(entries));
        Items = changes.ApplyTo(Items);
        return changes;
    }

    public void Apply(ItemUpdate update) => Items = new ListChangeSet { ItemsToUpdate = { update } }.ApplyTo(Items);

    public void Apply(ListChangeSet changes) => Items = changes.ApplyTo(Items);

    public ListItem AddExtra(string name)
    {
        var extra = new ListItem { Id = Guid.NewGuid(), Name = name, IsExtra = true, Category = AisleCategory.DairyEggs };
        Items = [.. Items, extra];
        return extra;
    }

    public List<ListItem> Visible => Items.Where(i => i.IsVisible).ToList();

    public ListItem Line(string name, Dimension? dimension = null) =>
        Assert.Single(Visible, i => i.Name == name && (dimension is null || i.Key?.Dimension == dimension));

    public string Total(string name, Dimension? dimension = null) => ShoppingListRules.FormatTotal(Line(name, dimension));
}

public class ShoppingListWorkedExamples
{
    [Fact]
    public void Onions_from_two_dishes_merge_into_one_line_with_both_sources()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("brown onion", 1, null, category: AisleCategory.Produce)));
        var b = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("brown onion", 2, "each")));

        k.Build(Kitchen.Week1, a, b);

        var onion = k.Line("brown onion");
        Assert.Equal("3", k.Total("brown onion"));
        Assert.Equal(3m, onion.ActiveTotal);
        Assert.Equal(new[] { a.EntryId, b.EntryId }.Order(), onion.Sources.Select(s => s.EntryId).Order());
        Assert.Equal(AisleCategory.Produce, onion.Category);
    }

    [Fact]
    public void Tablespoons_and_millilitres_merge()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("soy sauce", 2, "tbsp"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("soy sauce", 100, "ml"))));

        Assert.Equal("140 ml", k.Total("soy sauce"));
    }

    [Fact]
    public void Mass_and_volume_of_the_same_ingredient_stay_separate()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("flour", 200, "g"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("flour", 1, "cup"))));

        Assert.Equal(2, k.Visible.Count(i => i.Name == "flour"));
        Assert.Equal("200 g", k.Total("flour", Dimension.Mass));
        Assert.Equal("250 ml", k.Total("flour", Dimension.Volume));
    }

    [Fact]
    public void Servings_override_scales_the_recipe()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken thigh fillets", 400, "g")), servings: 4));

        Assert.Equal("800 g", k.Total("chicken thigh fillets"));
        Assert.Equal(800m, k.Line("chicken thigh fillets").ActiveTotal);
    }

    [Fact]
    public void Removing_a_dish_and_rebuilding_drops_only_its_share()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("brown onion", 1, null), k.Ing("garlic", 2, "cloves")));
        var b = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("brown onion", 2, null), k.Ing("rice", 300, "g")));
        k.Build(Kitchen.Week1, a, b);
        var garlicBefore = k.Line("garlic");

        var changes = k.Build(Kitchen.Week1, a);

        Assert.Equal("1", k.Total("brown onion"));
        Assert.Equal("2 cloves", k.Total("garlic"));
        Assert.DoesNotContain(k.Items, i => i.Name == "rice");
        Assert.Equal(2, changes.SourcesToDelete.Count);
        Assert.Single(changes.ItemsToDelete);
        Assert.Empty(changes.ItemsToUpdate);
        Assert.Empty(changes.SourcesToAdd);
        Assert.Equal(garlicBefore.Id, k.Line("garlic").Id);
    }

    [Fact]
    public void Extras_survive_a_rebuild()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("rice", 300, "g")));
        k.Build(Kitchen.Week1, a);
        k.AddExtra("milk");

        k.Build(Kitchen.Week1, a);
        k.Build(Kitchen.Week1);

        Assert.Contains(k.Visible, i => i.Name == "milk" && i.IsExtra);
        Assert.DoesNotContain(k.Items, i => i.Name == "rice");
    }

    [Fact]
    public void Ticked_item_stays_ticked_after_a_rebuild_with_no_plan_change()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g")));
        k.Build(Kitchen.Week1, a);
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));

        var changes = k.Build(Kitchen.Week1, a);

        Assert.True(changes.IsEmpty);
        Assert.Equal(ItemState.Bought, k.Line("chicken").State);
    }

    [Fact]
    public void Salt_to_taste_in_three_dishes_is_one_check_you_have_line_without_quantity()
    {
        var k = new Kitchen();
        var salt = () => k.Ing("salt", null, null, "to taste", AisleCategory.Spices, staple: true);
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, salt())),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(4, salt())),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(1, salt())));

        var line = k.Line("salt");
        Assert.True(line.InCheckGroup);
        Assert.Null(line.ActiveTotal);
        Assert.Equal(3, line.Sources.Count);
        Assert.Equal("to taste", k.Total("salt"));
    }
}

public class ShoppingListRebuildRules
{
    [Fact]
    public void Optional_ingredients_are_left_off_the_list()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1,
            Kitchen.Recipe(2, k.Ing("rice", 300, "g"), k.Ing("coriander", 1, "bunch", optional: true))));

        Assert.DoesNotContain(k.Items, i => i.Name == "coriander");
    }

    [Fact]
    public void Count_labels_only_add_when_they_match()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("garlic", 2, "cloves"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("garlic", 3, "clove"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("garlic", 1, "bunch"))));

        var lines = k.Visible.Where(i => i.Name == "garlic").Select(ShoppingListRules.FormatTotal).Order().ToArray();
        Assert.Equal(["1 bunch", "5 cloves"], lines);
    }

    [Fact]
    public void Different_text_amounts_are_listed_as_written_and_de_duplicated()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1,
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("pepper", null, null, "to taste"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("pepper", null, null, "a pinch"))),
            Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("pepper", null, null, "To taste"))));

        Assert.Equal("to taste, a pinch", k.Total("pepper"));
    }

    [Fact]
    public void Same_ingredient_twice_in_one_recipe_gives_one_source_for_that_entry()
    {
        var k = new Kitchen();
        var entry = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("butter", 50, "g"), k.Ing("butter", 1, "tbsp"), k.Ing("butter", 20, "g")));
        k.Build(Kitchen.Week1, entry);

        var mass = k.Line("butter", Dimension.Mass);
        Assert.Single(mass.Sources);
        Assert.Equal(70m, mass.ActiveTotal);
        Assert.Equal(20m, k.Line("butter", Dimension.Volume).ActiveTotal);
    }

    [Fact]
    public void Bought_item_whose_amount_grows_returns_to_needed_and_is_marked()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g")));
        k.Build(Kitchen.Week1, a);
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        Assert.Equal(400m, k.Line("chicken").BoughtTotal);

        k.Build(Kitchen.Week1, a, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(4, k.Ing("chicken", 500, "g"))));

        var chicken = k.Line("chicken");
        Assert.Equal(ItemState.Needed, chicken.State);
        Assert.True(chicken.AmountIncreased);
        Assert.Equal(400m, chicken.BoughtTotal);
        Assert.Equal("900 g", k.Total("chicken"));

        k.Apply(ShoppingListRules.SetState(chicken, ItemState.Bought));
        Assert.False(k.Line("chicken").AmountIncreased);
        Assert.Equal(900m, k.Line("chicken").BoughtTotal);
    }

    [Fact]
    public void Bought_item_whose_amount_shrinks_stays_bought()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g")));
        var b = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 200, "g")));
        k.Build(Kitchen.Week1, a, b);
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));

        k.Build(Kitchen.Week1, a);

        Assert.Equal(ItemState.Bought, k.Line("chicken").State);
        Assert.Equal("400 g", k.Total("chicken"));
    }

    [Fact]
    public void Have_it_survives_a_rebuild()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("rice", 300, "g")));
        k.Build(Kitchen.Week1, a);
        k.Apply(ShoppingListRules.SetState(k.Line("rice"), ItemState.HaveIt));

        k.Build(Kitchen.Week1, a, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("rice", 100, "g"))));

        Assert.Equal(ItemState.HaveIt, k.Line("rice").State);
        Assert.Equal("400 g", k.Total("rice"));
    }

    [Fact]
    public void Servings_change_updates_the_source_in_place()
    {
        var k = new Kitchen();
        var recipe = Kitchen.Recipe(2, k.Ing("chicken", 400, "g"));
        var entryId = Guid.NewGuid();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, recipe, id: entryId));
        var sourceId = k.Line("chicken").Sources.Single().Id;

        var changes = k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, recipe, servings: 3, id: entryId));

        Assert.Single(changes.SourcesToUpdate);
        Assert.Equal(sourceId, k.Line("chicken").Sources.Single().Id);
        Assert.Equal("600 g", k.Total("chicken"));
    }

    [Fact]
    public void Building_a_new_week_adds_to_what_remains_and_leaves_the_old_week_alone()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("brown onion", 1, null))));
        k.Build(Kitchen.Week2, Kitchen.Entry(Kitchen.Week2, Kitchen.Recipe(2, k.Ing("brown onion", 2, null), k.Ing("rice", 200, "g"))));

        Assert.Equal("3", k.Total("brown onion"));

        // Rebuilding week 2 with nothing planned only removes week 2's shares.
        k.Build(Kitchen.Week2);
        Assert.Equal("1", k.Total("brown onion"));
        Assert.DoesNotContain(k.Items, i => i.Name == "rice");
    }

    [Fact]
    public void Ingredient_rename_and_category_change_flow_through_on_rebuild()
    {
        var k = new Kitchen();
        var ingId = k.Ing("capsicum", 1, null).IngredientId;
        var entryId = Guid.NewGuid();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("capsicum", 1, null)), id: entryId));

        var renamed = new PlannedIngredient(ingId, "red capsicum", AisleCategory.Produce, false, 1, null, null, false);
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, renamed), id: entryId));

        var line = Assert.Single(k.Visible);
        Assert.Equal("red capsicum", line.Name);
        Assert.Equal(AisleCategory.Produce, line.Category);
    }
}

public class ClearCompletedRules
{
    [Fact]
    public void Clearing_removes_bought_and_have_it_lines_and_completed_extras()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g"), k.Ing("rice", 300, "g"), k.Ing("oil", 1, "tbsp")));
        k.Build(Kitchen.Week1, a);
        var milk = k.AddExtra("milk");
        var bread = k.AddExtra("bread");
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        k.Apply(ShoppingListRules.SetState(k.Line("oil"), ItemState.HaveIt));
        k.Apply(ShoppingListRules.SetState(k.Items.Single(i => i.Id == milk.Id), ItemState.Bought));

        k.Apply(ShoppingListRules.ClearCompleted(k.Items));

        Assert.Equal(["bread", "rice"], k.Visible.Select(i => i.Name).Order().ToArray());
        Assert.DoesNotContain(k.Items, i => i.Id == milk.Id);
        Assert.Contains(k.Items, i => i.Id == bread.Id);
    }

    [Fact]
    public void Rebuilding_the_same_week_after_clearing_does_not_bring_items_back()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g")));
        k.Build(Kitchen.Week1, a);
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        k.Apply(ShoppingListRules.ClearCompleted(k.Items));

        k.Build(Kitchen.Week1, a);

        Assert.Empty(k.Visible);
    }

    [Fact]
    public void Next_week_after_clearing_shows_only_the_new_amount_as_needed()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g"))));
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        k.Apply(ShoppingListRules.ClearCompleted(k.Items));

        k.Build(Kitchen.Week2, Kitchen.Entry(Kitchen.Week2, Kitchen.Recipe(2, k.Ing("chicken", 300, "g"))));

        var chicken = k.Line("chicken");
        Assert.Equal(ItemState.Needed, chicken.State);
        Assert.False(chicken.AmountIncreased);
        Assert.Equal("300 g", k.Total("chicken"));
    }

    [Fact]
    public void Adding_a_dish_to_a_cleared_week_shows_only_that_dish()
    {
        var k = new Kitchen();
        var a = Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g")));
        k.Build(Kitchen.Week1, a);
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        k.Apply(ShoppingListRules.ClearCompleted(k.Items));

        k.Build(Kitchen.Week1, a, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 250, "g"))));

        Assert.Equal("250 g", k.Total("chicken"));
        Assert.Equal(ItemState.Needed, k.Line("chicken").State);
    }

    [Fact]
    public void Removing_a_cleared_dish_eventually_deletes_the_row()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("chicken", 400, "g"))));
        k.Apply(ShoppingListRules.SetState(k.Line("chicken"), ItemState.Bought));
        k.Apply(ShoppingListRules.ClearCompleted(k.Items));

        k.Build(Kitchen.Week1);

        Assert.Empty(k.Items);
    }

    [Fact]
    public void Staple_moved_to_needed_leaves_check_group()
    {
        var k = new Kitchen();
        k.Build(Kitchen.Week1, Kitchen.Entry(Kitchen.Week1, Kitchen.Recipe(2, k.Ing("olive oil", 2, "tbsp", staple: true))));
        Assert.True(k.Line("olive oil").InCheckGroup);

        k.Apply(ShoppingListRules.SetInCheckGroup(k.Line("olive oil"), false));

        Assert.False(k.Line("olive oil").InCheckGroup);
    }
}
