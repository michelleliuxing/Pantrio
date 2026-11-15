using System.Net;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Features.Recipes;
using Pantrio.Api.Tests.Infrastructure;
using Pantrio.Domain;

namespace Pantrio.Api.Tests;

internal static class Recipes
{
    public static object Katsu(string name = "Chicken katsu curry") => new
    {
        name,
        servings = 2,
        tags = new[] { "Japanese", "weeknight" },
        prepMins = 40,
        notes = "kids love it",
        ingredients = new object[]
        {
            new { name = "Chicken thigh fillets", quantity = 400m, unit = "g", optional = false },
            new { name = "brown onion", quantity = 1m, unit = (string?)null, optional = false },
            new { name = "Japanese curry roux", quantity = 100m, unit = "grams", optional = false },
            new { name = "Salt", quantity = (decimal?)null, quantityText = "to taste", optional = false },
            new { name = "Pickled ginger", quantity = 1m, unit = "tbsp", optional = true, category = AisleCategory.International },
        },
    };

    public static async Task<RecipeDto> Create(HttpClient client, object? body = null) =>
        await (await client.PostJson("/api/recipes", body ?? Katsu())).ReadAs<RecipeDto>();
}

[Collection(ApiCollection.Name)]
public class RecipeTests(PantrioApiFactory factory)
{
    [Fact]
    public async Task New_household_has_a_seeded_catalogue()
    {
        var client = await factory.SignedIn();
        var items = await client.GetAs<List<IngredientDto>>("/api/ingredients?search=capsicum");
        var capsicum = Assert.Single(items, i => i.Name == "capsicum");
        Assert.Equal(AisleCategory.Produce, capsicum.Category);
    }

    [Fact]
    public async Task Ingredient_search_matches_aliases_and_typos()
    {
        var client = await factory.SignedIn();
        Assert.Contains(await client.GetAs<List<IngredientDto>>("/api/ingredients?search=cilantro"), i => i.Name == "coriander");
        Assert.Contains(await client.GetAs<List<IngredientDto>>("/api/ingredients?search=zuchini"), i => i.Name == "zucchini");
    }

    [Fact]
    public async Task Saving_a_recipe_links_ingredients_to_the_catalogue_and_normalises_units()
    {
        var client = await factory.SignedIn();

        var recipe = await Recipes.Create(client);

        Assert.Equal(["japanese", "weeknight"], recipe.Tags);
        Assert.Equal(5, recipe.Ingredients.Count);
        var chicken = recipe.Ingredients[0];
        Assert.Equal("chicken thigh fillets", chicken.Name); // matched the seeded entry, not a new one
        Assert.Equal(AisleCategory.MeatSeafood, chicken.Category);
        Assert.Equal("g", recipe.Ingredients[2].Unit);
        Assert.Equal("each", recipe.Ingredients[1].Unit);
        var salt = recipe.Ingredients[3];
        Assert.Null(salt.Quantity);
        Assert.Null(salt.Unit);
        Assert.Equal("to taste", salt.QuantityText);
        Assert.True(salt.IsStaple);
        Assert.True(recipe.Ingredients[4].Optional);
        Assert.Equal(Enumerable.Range(0, 5), recipe.Ingredients.Select(i => i.SortOrder));
    }

    [Fact]
    public async Task Reopening_a_recipe_returns_exactly_what_was_saved()
    {
        var client = await factory.SignedIn();
        var created = await Recipes.Create(client);

        var reopened = await client.GetAs<RecipeDto>($"/api/recipes/{created.Id}");

        Assert.Equal(created.Ingredients.Select(i => (i.IngredientId, i.Quantity, i.Unit, i.QuantityText, i.Optional, i.SortOrder)),
            reopened.Ingredients.Select(i => (i.IngredientId, i.Quantity, i.Unit, i.QuantityText, i.Optional, i.SortOrder)));
    }

    [Fact]
    public async Task Put_replaces_the_ingredient_list_as_a_whole()
    {
        var client = await factory.SignedIn();
        var created = await Recipes.Create(client);
        var rows = created.Ingredients.Select(i => new
        {
            ingredientId = (Guid?)i.IngredientId,
            name = i.Name,
            quantity = i.Quantity,
            unit = i.Unit,
            quantityText = i.QuantityText,
            optional = i.Optional,
        }).ToList();
        rows.RemoveAt(4); // delete one
        rows[0] = rows[0] with { quantity = 500m }; // edit one
        rows.Insert(0, new { ingredientId = (Guid?)null, name = "Panko breadcrumbs", quantity = (decimal?)1m, unit = (string?)"cup", quantityText = (string?)null, optional = false }); // add one

        var updated = await (await client.PutJson($"/api/recipes/{created.Id}", new
        {
            name = "Chicken katsu curry",
            servings = 2,
            ingredients = rows,
        })).ReadAs<RecipeDto>();

        Assert.Equal(["panko breadcrumbs", "chicken thigh fillets", "brown onion", "japanese curry roux", "salt"], updated.Ingredients.Select(i => i.Name));
        Assert.Equal(500m, updated.Ingredients[1].Quantity);
        var reopened = await client.GetAs<RecipeDto>($"/api/recipes/{created.Id}");
        Assert.Equal(5, reopened.Ingredients.Count);
    }

    [Fact]
    public async Task New_ingredient_names_create_catalogue_entries_once()
    {
        var client = await factory.SignedIn();
        var recipe = await Recipes.Create(client, new
        {
            name = "Dragonfruit bowl",
            servings = 1,
            ingredients = new object[]
            {
                new { name = "Dragon fruit", quantity = 1m, category = AisleCategory.Produce, optional = false },
                new { name = "dragon  fruit", quantity = 1m, optional = false },
            },
        });

        Assert.Equal(recipe.Ingredients[0].IngredientId, recipe.Ingredients[1].IngredientId);
        Assert.Equal(AisleCategory.Produce, recipe.Ingredients[0].Category);
        Assert.Single(await client.GetAs<List<IngredientDto>>("/api/ingredients?search=dragon fruit"), i => i.Name == "Dragon fruit");
    }

    [Fact]
    public async Task Unknown_units_are_rejected_with_a_field_error()
    {
        var client = await factory.SignedIn();
        var res = await client.PostJson("/api/recipes", new
        {
            name = "Bad",
            servings = 2,
            ingredients = new object[] { new { name = "flour", quantity = 2m, unit = "2 cups", optional = false } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("ingredients[0].unit", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_tag_filter_favourites_duplicate_and_delete()
    {
        var client = await factory.SignedIn();
        var katsu = await Recipes.Create(client);
        var soup = await Recipes.Create(client, new
        {
            name = "Pumpkin soup",
            servings = 4,
            tags = new[] { "soup" },
            ingredients = new object[] { new { name = "pumpkin", quantity = 1m, unit = "kg", optional = false } },
        });

        Assert.Equal([soup.Id], (await client.GetAs<List<RecipeSummaryDto>>("/api/recipes?search=pump")).Select(r => r.Id));
        Assert.Equal([katsu.Id], (await client.GetAs<List<RecipeSummaryDto>>("/api/recipes?tag=japanese")).Select(r => r.Id));

        await (await client.PatchJson($"/api/recipes/{soup.Id}", new { isFavourite = true })).ReadAs<RecipeSummaryDto>();
        Assert.Equal([soup.Id], (await client.GetAs<List<RecipeSummaryDto>>("/api/recipes?favourites=true")).Select(r => r.Id));

        var copy = await (await client.PostJson($"/api/recipes/{katsu.Id}/duplicate")).ReadAs<RecipeDto>();
        Assert.Equal("Chicken katsu curry (copy)", copy.Name);
        Assert.Equal(katsu.Ingredients.Select(i => i.IngredientId), copy.Ingredients.Select(i => i.IngredientId));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{katsu.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{katsu.Id}")).StatusCode);

        var tags = await client.GetAs<List<TagCount>>("/api/recipes/tags");
        Assert.Contains(tags, t => t.Tag == "soup" && t.Count == 1);
    }
}

[Collection(ApiCollection.Name)]
public class HouseholdIsolationTests(PantrioApiFactory factory)
{
    [Fact]
    public async Task Another_household_cannot_read_or_change_a_recipe_by_id()
    {
        var alice = await factory.SignedIn();
        var bob = await factory.SignedIn();
        var recipe = await Recipes.Create(alice);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/recipes/{recipe.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutJson($"/api/recipes/{recipe.Id}", Recipes.Katsu("Stolen"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchJson($"/api/recipes/{recipe.Id}", new { isFavourite = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostJson($"/api/recipes/{recipe.Id}/duplicate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/recipes/{recipe.Id}")).StatusCode);
        Assert.DoesNotContain(await bob.GetAs<List<RecipeSummaryDto>>("/api/recipes"), r => r.Id == recipe.Id);

        var stillThere = await alice.GetAs<RecipeDto>($"/api/recipes/{recipe.Id}");
        Assert.Equal("Chicken katsu curry", stillThere.Name);
        Assert.False(stillThere.IsFavourite);
    }

    [Fact]
    public async Task Another_household_cannot_link_or_edit_a_catalogue_entry_by_id()
    {
        var alice = await factory.SignedIn();
        var bob = await factory.SignedIn();
        var aliceRecipe = await Recipes.Create(alice);
        var aliceChicken = aliceRecipe.Ingredients[0].IngredientId;

        var bobRecipe = await Recipes.Create(bob, new
        {
            name = "Borrowed",
            servings = 2,
            ingredients = new object[] { new { ingredientId = aliceChicken, name = "chicken thigh fillets", quantity = 1m, unit = "kg", optional = false } },
        });

        Assert.NotEqual(aliceChicken, bobRecipe.Ingredients[0].IngredientId);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PatchJson($"/api/ingredients/{aliceChicken}", new { name = "hacked" })).StatusCode);
        Assert.DoesNotContain(await bob.GetAs<List<IngredientDto>>("/api/ingredients?limit=200"), i => i.Id == aliceChicken);
    }
}
