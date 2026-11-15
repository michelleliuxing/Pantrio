using System.Net;
using System.Net.Http.Headers;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Features.Recipes;
using Pantrio.Api.Features.ShoppingList;
using Pantrio.Api.Tests.Infrastructure;

namespace Pantrio.Api.Tests;

[Collection(ApiCollection.Name)]
public class PurchaseLinkTests(PantrioApiFactory factory)
{
    private const string Woolies = "https://www.woolworths.com.au/shop/productdetails/123456/japanese-curry-roux";

    private static object Row(string name, decimal? qty, string? unit, string? purchaseUrl = null, Guid? ingredientId = null) =>
        new { ingredientId, name, quantity = qty, unit, optional = false, purchaseUrl };

    [Fact]
    public async Task Recipe_editor_rows_set_and_clear_the_catalogue_buy_link()
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c, new { name = "Curry", servings = 2, ingredients = new[] { Row("japanese curry roux", 100, "g", Woolies) } });
        Assert.Equal(Woolies, recipe.Ingredients[0].PurchaseUrl);

        // Another recipe using the same catalogue entry sees the link, and omitting the field leaves it alone.
        var other = await Recipes.Create(c, new { name = "Curry udon", servings = 2, ingredients = new[] { Row("japanese curry roux", 50, "g") } });
        Assert.Equal(Woolies, other.Ingredients[0].PurchaseUrl);
        var found = await c.GetAs<List<IngredientDto>>("/api/ingredients?search=curry roux");
        Assert.Equal(Woolies, Assert.Single(found, i => i.Name == "japanese curry roux").PurchaseUrl);

        // An empty string clears it.
        var cleared = await (await c.PutJson($"/api/recipes/{recipe.Id}", new { name = "Curry", servings = 2, ingredients = new[] { Row("japanese curry roux", 100, "g", "") } }))
            .ReadAs<RecipeDto>();
        Assert.Null(cleared.Ingredients[0].PurchaseUrl);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("woolworths.com.au/milk")]
    [InlineData("ftp://example.com/x")]
    public async Task Only_http_links_are_accepted(string url)
    {
        var c = await factory.SignedIn();
        var res = await c.PostJson("/api/recipes", new { name = "Bad link", servings = 2, ingredients = new[] { Row("milk", 1, "l", url) } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("ingredients[0].purchaseUrl", await res.Content.ReadAsStringAsync());

        var ingredient = Assert.Single(await c.GetAs<List<IngredientDto>>("/api/ingredients?search=milk"), i => i.Name == "milk");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchJson($"/api/ingredients/{ingredient.Id}", new { purchaseUrl = url })).StatusCode);
    }

    [Fact]
    public async Task Shopping_list_shows_the_live_link_for_recipe_lines_and_matching_extras()
    {
        var c = await factory.SignedIn();
        var recipe = await Kit.Recipe(c, "Curry", 2, Kit.Ing("japanese curry roux", 100, "g"));
        await Kit.Plan(c, recipe);
        await Kit.Build(c);
        await c.PostJson("/api/shopping-list/items", new { name = "Milk" });

        var catalogue = await c.GetAs<List<IngredientDto>>("/api/ingredients?limit=200");
        var roux = catalogue.Single(i => i.Name == "japanese curry roux");
        var milk = catalogue.Single(i => i.Name == "milk");
        await (await c.PatchJson($"/api/ingredients/{roux.Id}", new { purchaseUrl = Woolies })).ReadAs<IngredientDto>();
        await (await c.PatchJson($"/api/ingredients/{milk.Id}", new { purchaseUrl = "https://www.coles.com.au/product/milk-2l" })).ReadAs<IngredientDto>();

        var list = await c.GetAs<ShoppingListDto>("/api/shopping-list"); // no rebuild needed
        var line = list.Line("japanese curry roux");
        Assert.Equal(roux.Id, line.IngredientId);
        Assert.Equal(Woolies, line.PurchaseUrl);
        Assert.Equal("https://www.coles.com.au/product/milk-2l", list.Line("Milk").PurchaseUrl);
    }
}

[Collection(ApiCollection.Name)]
public class RecipeImageTests(PantrioApiFactory factory)
{
    // Smallest valid PNG (1x1), plus JPEG and WebP signatures for type detection.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static MultipartFormDataContent Form(byte[] bytes, string contentType = "image/png", string fileName = "photo.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Upload_replace_serve_and_delete_a_recipe_photo()
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c);
        Assert.Null(recipe.ImageUrl);

        var uploaded = await (await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Png))).ReadAs<RecipeImageDto>();
        Assert.StartsWith($"/api/recipes/{recipe.Id}/image?v=", uploaded.ImageUrl);

        var served = await c.GetAsync(uploaded.ImageUrl);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await served.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("immutable", served.Headers.CacheControl!.ToString());

        Assert.Equal(uploaded.ImageUrl, (await c.GetAs<RecipeDto>($"/api/recipes/{recipe.Id}")).ImageUrl);
        Assert.Equal(uploaded.ImageUrl, (await c.GetAs<List<RecipeSummaryDto>>("/api/recipes")).Single().ImageUrl);

        // Replacing changes the version, so caches pick up the new photo. Type comes from the bytes, not the header.
        var replaced = await (await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Jpeg, "image/png", "lies.png"))).ReadAs<RecipeImageDto>();
        Assert.NotEqual(uploaded.ImageUrl, replaced.ImageUrl);
        Assert.Equal("image/jpeg", (await c.GetAsync(replaced.ImageUrl)).Content.Headers.ContentType?.MediaType);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/recipes/{recipe.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(replaced.ImageUrl)).StatusCode);
        Assert.Null((await c.GetAs<RecipeDto>($"/api/recipes/{recipe.Id}")).ImageUrl);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>", "image/svg+xml")]
    [InlineData("<html><script>alert(1)</script></html>", "image/png")]
    [InlineData("GIF89a....", "image/gif")]
    public async Task Non_photo_files_are_refused(string body, string contentType)
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c);

        var res = await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(System.Text.Encoding.UTF8.GetBytes(body), contentType, "x"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Photos_over_2_mb_are_refused()
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c);
        var big = new byte[RecipeImageEndpoints.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);

        var res = await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(big, "image/jpeg", "big.jpg"));

        Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, res.StatusCode.ToString());
    }

    [Fact]
    public async Task Duplicating_copies_the_photo_and_deleting_the_recipe_removes_it()
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c);
        await (await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Png))).ReadAs<RecipeImageDto>();

        var copy = await (await c.PostJson($"/api/recipes/{recipe.Id}/duplicate")).ReadAs<RecipeDto>();
        Assert.NotNull(copy.ImageUrl);
        Assert.Equal(Png, await (await c.GetAsync(copy.ImageUrl)).Content.ReadAsByteArrayAsync());

        await c.DeleteAsync($"/api/recipes/{recipe.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/recipes/{recipe.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(copy.ImageUrl)).StatusCode);
    }

    [Fact]
    public async Task Upload_needs_the_csrf_header()
    {
        var c = await factory.SignedIn();
        var recipe = await Recipes.Create(c);
        c.DefaultRequestHeaders.Remove("X-Pantrio-Csrf");

        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Png))).StatusCode);
    }

    [Fact]
    public async Task Another_household_cannot_see_replace_or_delete_a_photo()
    {
        var alice = await factory.SignedIn();
        var bob = await factory.SignedIn();
        var recipe = await Recipes.Create(alice);
        var url = (await (await alice.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Png))).ReadAs<RecipeImageDto>()).ImageUrl;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PutAsync($"/api/recipes/{recipe.Id}/image", Form(Jpeg, "image/jpeg", "x.jpg"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/recipes/{recipe.Id}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Anonymous().GetAsync(url)).StatusCode);
        Assert.Equal(Png, await (await alice.GetAsync(url)).Content.ReadAsByteArrayAsync());
    }
}
