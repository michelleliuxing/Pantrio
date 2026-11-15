using System.Net;
using Pantrio.Api.Features.MealPlans;
using Pantrio.Api.Features.Recipes;
using Pantrio.Api.Features.ShoppingList;
using Pantrio.Api.Tests.Infrastructure;
using Pantrio.Domain;
using Pantrio.Domain.Quantities;
using Pantrio.Domain.ShoppingList;

namespace Pantrio.Api.Tests;

internal static class Kit
{
    public const string Week = "2030-01-07"; // a Monday
    public const string NextWeek = "2030-01-14";

    public static object Ing(string name, decimal? qty, string? unit = null, string? text = null, bool optional = false) =>
        new { name, quantity = qty, unit, quantityText = text, optional };

    public static Task<RecipeDto> Recipe(HttpClient c, string name, int servings, params object[] ingredients) =>
        Recipes.Create(c, new { name, servings, ingredients });

    public static async Task<MealPlanEntryDto> Plan(HttpClient c, RecipeDto recipe, string week = Week, string? day = null,
        MealSlot? slot = null, int? servings = null) =>
        await (await c.PostJson($"/api/meal-plans/{week}/entries", new { recipeId = recipe.Id, day, mealSlot = slot, servingsOverride = servings }))
            .ReadAs<MealPlanEntryDto>();

    public static async Task<ShoppingListDto> Build(HttpClient c, string week = Week) =>
        await (await c.PostJson("/api/shopping-list/build", new { weekStart = week })).ReadAs<ShoppingListDto>();

    public static ShoppingListItemDto Line(this ShoppingListDto list, string name, Dimension? dim = null) =>
        Assert.Single(list.Items, i => i.Name == name && (dim is null || i.Dimension == dim));
}

[Collection(ApiCollection.Name)]
public class MealPlanTests(PantrioApiFactory factory)
{
    [Fact]
    public async Task Empty_week_has_no_plan_yet()
    {
        var c = await factory.SignedIn();
        var plan = await c.GetAs<MealPlanDto>($"/api/meal-plans/{Kit.Week}");
        Assert.Null(plan.Id);
        Assert.Empty(plan.Entries);
    }

    [Fact]
    public async Task Weeks_start_on_monday()
    {
        var c = await factory.SignedIn();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/meal-plans/2030-01-08")).StatusCode);
    }

    [Fact]
    public async Task Dishes_go_to_the_cart_or_a_slot_and_can_move_and_change_servings()
    {
        var c = await factory.SignedIn();
        var katsu = await Recipes.Create(c);

        var inCart = await Kit.Plan(c, katsu);
        var placed = await Kit.Plan(c, katsu, day: "2030-01-09", slot: MealSlot.Dinner, servings: 4);
        Assert.Null(inCart.Day);
        Assert.Equal(4, placed.ServingsOverride);

        var moved = await (await c.PutJson($"/api/meal-plan-entries/{inCart.Id}", new { day = "2030-01-13", mealSlot = MealSlot.Lunch, servingsOverride = 2 }))
            .ReadAs<MealPlanEntryDto>();
        Assert.Equal(new DateOnly(2030, 1, 13), moved.Day);
        Assert.Null(moved.ServingsOverride); // same as the recipe's servings: no override stored

        var plan = await c.GetAs<MealPlanDto>($"/api/meal-plans/{Kit.Week}");
        Assert.Equal(2, plan.Entries.Count);
        Assert.All(plan.Entries, e => Assert.Equal("Chicken katsu curry", e.RecipeName));

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/meal-plan-entries/{placed.Id}")).StatusCode);
        Assert.Single((await c.GetAs<MealPlanDto>($"/api/meal-plans/{Kit.Week}")).Entries);
    }

    [Fact]
    public async Task Placement_must_be_inside_the_week_and_complete()
    {
        var c = await factory.SignedIn();
        var katsu = await Recipes.Create(c);
        var outside = await c.PostJson($"/api/meal-plans/{Kit.Week}/entries", new { recipeId = katsu.Id, day = "2030-01-20", mealSlot = MealSlot.Dinner });
        var half = await c.PostJson($"/api/meal-plans/{Kit.Week}/entries", new { recipeId = katsu.Id, day = "2030-01-08" });
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
    }

    [Fact]
    public async Task Copy_last_week_shifts_days_by_seven()
    {
        var c = await factory.SignedIn();
        var katsu = await Recipes.Create(c);
        await Kit.Plan(c, katsu, day: "2030-01-09", slot: MealSlot.Dinner);
        await Kit.Plan(c, katsu);

        var copied = await (await c.PostJson($"/api/meal-plans/{Kit.NextWeek}/copy-previous")).ReadAs<MealPlanDto>();

        Assert.Equal(2, copied.Entries.Count);
        Assert.Contains(copied.Entries, e => e.Day == new DateOnly(2030, 1, 16) && e.MealSlot == MealSlot.Dinner);
        Assert.Contains(copied.Entries, e => e.Day is null);
    }

    [Fact]
    public async Task Another_household_cannot_touch_plan_entries_or_plan_foreign_recipes()
    {
        var alice = await factory.SignedIn();
        var bob = await factory.SignedIn();
        var katsu = await Recipes.Create(alice);
        var entry = await Kit.Plan(alice, katsu);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutJson($"/api/meal-plan-entries/{entry.Id}", new { day = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/meal-plan-entries/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostJson($"/api/meal-plans/{Kit.Week}/entries", new { recipeId = katsu.Id })).StatusCode);
        Assert.Empty((await bob.GetAs<MealPlanDto>($"/api/meal-plans/{Kit.Week}")).Entries);
        Assert.Single((await alice.GetAs<MealPlanDto>($"/api/meal-plans/{Kit.Week}")).Entries);
    }
}

[Collection(ApiCollection.Name)]
public class ShoppingListApiTests(PantrioApiFactory factory)
{
    private sealed record Fixture(HttpClient Client, MealPlanEntryDto A, MealPlanEntryDto B, MealPlanEntryDto Chicken);

    /// <summary>Dish A and B overlap on onions, soy sauce, flour and salt; the chicken dish is overridden from 2 to 4 servings.</summary>
    private async Task<Fixture> PlanTheWorkedExamples()
    {
        var c = await factory.SignedIn();
        var a = await Kit.Recipe(c, "Dish A", 2,
            Kit.Ing("brown onion", 1), Kit.Ing("soy sauce", 2, "tbsp"), Kit.Ing("plain flour", 200, "g"), Kit.Ing("salt", null, text: "to taste"),
            Kit.Ing("coriander", 1, "bunch", optional: true));
        var b = await Kit.Recipe(c, "Dish B", 2,
            Kit.Ing("brown onion", 2, "each"), Kit.Ing("soy sauce", 100, "ml"), Kit.Ing("plain flour", 1, "cup"), Kit.Ing("salt", null, text: "to taste"));
        var chicken = await Kit.Recipe(c, "Roast chicken", 2, Kit.Ing("chicken thigh fillets", 400, "g"), Kit.Ing("salt", null, text: "to taste"));

        return new Fixture(c,
            await Kit.Plan(c, a, day: "2030-01-07", slot: MealSlot.Dinner),
            await Kit.Plan(c, b), // in the cart: still feeds the list
            await Kit.Plan(c, chicken, day: "2030-01-08", slot: MealSlot.Lunch, servings: 4));
    }

    [Fact]
    public async Task Building_merges_scales_and_groups_like_the_worked_examples()
    {
        var f = await PlanTheWorkedExamples();

        var list = await Kit.Build(f.Client);

        var onion = list.Line("brown onion");
        Assert.Equal("3", onion.Amount);
        Assert.Equal(["Dish A", "Dish B"], onion.Sources.Select(s => s.RecipeName));
        Assert.Equal(AisleCategory.Produce, onion.Category);
        Assert.Equal("140 ml", list.Line("soy sauce").Amount);
        Assert.Equal("200 g", list.Line("plain flour", Dimension.Mass).Amount);
        Assert.Equal("250 ml", list.Line("plain flour", Dimension.Volume).Amount);
        Assert.Equal("800 g", list.Line("chicken thigh fillets").Amount);
        var salt = list.Line("salt");
        Assert.True(salt.InCheckGroup);
        Assert.Null(salt.Total);
        Assert.Equal("to taste", salt.Amount);
        Assert.Equal(3, salt.Sources.Count);
        Assert.DoesNotContain(list.Items, i => i.Name == "coriander");
    }

    [Fact]
    public async Task Rebuild_after_removing_a_dish_keeps_ticks_and_extras()
    {
        var f = await PlanTheWorkedExamples();
        var c = f.Client;
        var list = await Kit.Build(c);
        await (await c.PostJson("/api/shopping-list/items", new { name = "milk" })).ReadAs<ShoppingListItemDto>();
        await (await c.PostJson("/api/shopping-list/items", new { name = "dishwashing liquid", quantityText = "1 bottle" })).ReadAs<ShoppingListItemDto>();
        await (await c.PatchJson($"/api/shopping-list/items/{list.Line("chicken thigh fillets").Id}", new { state = ItemState.Bought })).ReadAs<ShoppingListItemDto>();

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/meal-plan-entries/{f.B.Id}")).StatusCode);
        list = await Kit.Build(c);

        Assert.Equal("1", list.Line("brown onion").Amount);
        Assert.Equal("40 ml", list.Line("soy sauce").Amount);
        Assert.DoesNotContain(list.Items, i => i.Name == "plain flour" && i.Dimension == Dimension.Volume);
        Assert.Equal(ItemState.Bought, list.Line("chicken thigh fillets").State);
        var milk = list.Line("milk");
        Assert.True(milk.IsExtra);
        Assert.Equal(AisleCategory.DairyEggs, milk.Category); // aisle taken from the catalogue
        Assert.Equal("1 bottle", list.Line("dishwashing liquid").Amount);
        Assert.Equal(2, list.Line("salt").Sources.Count);
    }

    [Fact]
    public async Task Rebuilding_with_no_plan_change_changes_nothing()
    {
        var f = await PlanTheWorkedExamples();
        var first = await Kit.Build(f.Client);
        await f.Client.PatchJson($"/api/shopping-list/items/{first.Line("chicken thigh fillets").Id}", new { state = ItemState.Bought });

        var second = await Kit.Build(f.Client);

        Assert.Equal(first.Items.Select(i => (i.Id, i.Amount)), second.Items.Select(i => (i.Id, i.Amount)));
        Assert.Equal(ItemState.Bought, second.Line("chicken thigh fillets").State);
    }

    [Fact]
    public async Task Bought_item_returns_to_needed_when_its_amount_grows()
    {
        var f = await PlanTheWorkedExamples();
        var c = f.Client;
        var list = await Kit.Build(c);
        await c.PatchJson($"/api/shopping-list/items/{list.Line("chicken thigh fillets").Id}", new { state = ItemState.Bought });

        await c.PutJson($"/api/meal-plan-entries/{f.Chicken.Id}", new { day = "2030-01-08", mealSlot = MealSlot.Lunch, servingsOverride = 6 });
        list = await Kit.Build(c);

        var chicken = list.Line("chicken thigh fillets");
        Assert.Equal(ItemState.Needed, chicken.State);
        Assert.True(chicken.AmountIncreased);
        Assert.Equal("800 g", chicken.BoughtAmount);
        Assert.Equal("1.2 kg", chicken.Amount);
    }

    [Fact]
    public async Task List_is_a_snapshot_until_rebuilt()
    {
        var c = await factory.SignedIn();
        var soup = await Kit.Recipe(c, "Soup", 2, Kit.Ing("pumpkin", 1, "kg"));
        await Kit.Plan(c, soup);
        await Kit.Build(c);

        await c.PutJson($"/api/recipes/{soup.Id}", new { name = "Soup", servings = 2, ingredients = new[] { Kit.Ing("pumpkin", 2, "kg") } });
        Assert.Equal("1 kg", (await c.GetAs<ShoppingListDto>("/api/shopping-list")).Line("pumpkin").Amount);

        Assert.Equal("2 kg", (await Kit.Build(c)).Line("pumpkin").Amount);
    }

    [Fact]
    public async Task Clearing_bought_items_survives_a_rebuild_and_next_week_adds_fresh()
    {
        var f = await PlanTheWorkedExamples();
        var c = f.Client;
        var list = await Kit.Build(c);
        await c.PatchJson($"/api/shopping-list/items/{list.Line("chicken thigh fillets").Id}", new { state = ItemState.Bought });
        await c.PatchJson($"/api/shopping-list/items/{list.Line("salt").Id}", new { state = ItemState.HaveIt });

        list = await (await c.PostJson("/api/shopping-list/clear-bought")).ReadAs<ShoppingListDto>();
        Assert.DoesNotContain(list.Items, i => i.Name is "chicken thigh fillets" or "salt");

        list = await Kit.Build(c);
        Assert.DoesNotContain(list.Items, i => i.Name == "chicken thigh fillets");

        var roast = await Kit.Recipe(c, "Another roast", 2, Kit.Ing("chicken thigh fillets", 300, "g"));
        await Kit.Plan(c, roast, week: Kit.NextWeek);
        list = await Kit.Build(c, Kit.NextWeek);
        var chicken = list.Line("chicken thigh fillets");
        Assert.Equal("300 g", chicken.Amount);
        Assert.Equal(ItemState.Needed, chicken.State);
        Assert.Equal("3", list.Line("brown onion").Amount); // last week's unbought onions remain
    }

    [Fact]
    public async Task Staples_can_move_to_needed_and_recipe_lines_cannot_be_deleted()
    {
        var f = await PlanTheWorkedExamples();
        var c = f.Client;
        var list = await Kit.Build(c);

        var salt = await (await c.PatchJson($"/api/shopping-list/items/{list.Line("salt").Id}", new { inCheckGroup = false })).ReadAs<ShoppingListItemDto>();
        Assert.False(salt.InCheckGroup);
        Assert.False((await Kit.Build(c)).Line("salt").InCheckGroup);

        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"/api/shopping-list/items/{salt.Id}")).StatusCode);
        var extra = await (await c.PostJson("/api/shopping-list/items", new { name = "paper towel" })).ReadAs<ShoppingListItemDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/shopping-list/items/{extra.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_single_dish_can_go_straight_to_the_list()
    {
        var c = await factory.SignedIn();
        var soup = await Kit.Recipe(c, "Soup", 2, Kit.Ing("pumpkin", 1, "kg"));

        var list = await (await c.PostJson("/api/shopping-list/add-recipe", new { recipeId = soup.Id, servings = 4 })).ReadAs<ShoppingListDto>();

        Assert.Equal("2 kg", list.Line("pumpkin").Amount);
        var week = list.Line("pumpkin").Sources.Single().WeekStart;
        var plan = await c.GetAs<MealPlanDto>($"/api/meal-plans/{week:yyyy-MM-dd}");
        Assert.Contains(plan.Entries, e => e.RecipeId == soup.Id && e.Day is null);
    }

    [Fact]
    public async Task Another_household_cannot_see_or_change_the_list()
    {
        var f = await PlanTheWorkedExamples();
        var list = await Kit.Build(f.Client);
        var bob = await factory.SignedIn();
        var target = list.Line("brown onion").Id;

        Assert.Empty((await bob.GetAs<ShoppingListDto>("/api/shopping-list")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchJson($"/api/shopping-list/items/{target}", new { state = ItemState.Bought })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/shopping-list/items/{target}")).StatusCode);
        Assert.Empty((await Kit.Build(bob)).Items); // building bob's (empty) week touches nothing of alice's
        Assert.Equal(ItemState.Needed, (await f.Client.GetAs<ShoppingListDto>("/api/shopping-list")).Line("brown onion").State);
        Assert.Equal("3", (await f.Client.GetAs<ShoppingListDto>("/api/shopping-list")).Line("brown onion").Amount);
    }
}
