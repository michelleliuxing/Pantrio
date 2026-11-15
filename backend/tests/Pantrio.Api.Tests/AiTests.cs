using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pantrio.Api.Features.Ai;
using Pantrio.Api.Tests.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Ai;

namespace Pantrio.Api.Tests;

public class SuggestionParserTests
{
    [Fact]
    public void Valid_output_is_normalised()
    {
        var (servings, items) = SuggestionParser.Parse("""
            {"servings": 2, "ingredients": [
              {"name": "chicken thigh fillets", "quantity": 400, "unit": "g", "quantity_text": null, "category": "meat_seafood", "optional": false, "is_staple": false, "note": "or breast"},
              {"name": "salt", "quantity": null, "unit": null, "quantity_text": "to taste", "category": "spices", "optional": false, "is_staple": true, "note": null},
              {"name": "garlic", "quantity": 3, "unit": "cloves", "quantity_text": null, "category": "produce", "optional": false, "is_staple": false, "note": null}
            ]}
            """, 2);

        Assert.Equal(2, servings);
        Assert.Equal(3, items.Count);
        Assert.Equal(("g", 400m), (items[0].Unit, items[0].Quantity!.Value));
        Assert.Equal("meat_seafood", items[0].Category);
        Assert.Equal("to taste", items[1].QuantityText);
        Assert.True(items[1].IsStaple);
        Assert.Equal("clove", items[2].Unit);
    }

    [Fact]
    public void Unknown_units_become_text_and_bad_rows_are_dropped()
    {
        var (_, items) = SuggestionParser.Parse("""
            {"servings": 0, "ingredients": [
              {"name": "flour", "quantity": 2, "unit": "heaped mugs", "category": "pantry"},
              {"name": "", "quantity": 1, "unit": "g", "category": "pantry"},
              {"name": "flour", "quantity": 5, "unit": "g", "category": "pantry"},
              {"name": "mystery", "quantity": -3, "unit": "g", "category": "nonsense"}
            ]}
            """, 4);

        Assert.Equal(["flour", "mystery"], items.Select(i => i.Name));
        Assert.Null(items[0].Quantity);
        Assert.Equal("2 heaped mugs", items[0].QuantityText);
        Assert.Null(items[1].Quantity);
        Assert.Equal("other", items[1].Category);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"servings": 2}""")]
    [InlineData("""{"servings": 2, "ingredients": []}""")]
    [InlineData("""[1,2,3]""")]
    public void Unusable_output_throws(string json) =>
        Assert.Throws<SuggestionFailedException>(() => SuggestionParser.Parse(json, 2));

    [Fact]
    public void Schema_lists_every_aisle_and_requires_every_property()
    {
        var schema = SuggestionParser.JsonSchema;
        Assert.Contains("\"sauces_condiments\"", schema);
        Assert.Contains("\"additionalProperties\":false", schema);
        Assert.Contains("\"required\":[\"name\",\"quantity\",\"unit\",\"quantity_text\",\"category\",\"optional\",\"is_staple\",\"note\"]", schema);
    }
}

[Collection(ApiCollection.Name)]
public class AiSuggestionTests(PantrioApiFactory factory)
{
    private static object Ask(string dish = "Chicken katsu curry", string mode = "all", string[]? already = null) =>
        new { dishName = dish, servings = 2, notes = "no coriander", mode, alreadyListed = already };

    [Fact]
    public async Task Draft_links_known_ingredients_to_the_catalogue_and_saves_nothing()
    {
        var client = await factory.SignedIn();

        var draft = await (await client.PostJson("/api/ai/ingredient-suggestions", Ask())).ReadAs<IngredientSuggestionResponse>();

        Assert.True(draft.IsFake);
        var chicken = Assert.Single(draft.Ingredients, i => i.Name == "chicken thigh fillets");
        Assert.NotNull(chicken.IngredientId);
        Assert.Equal(AisleCategory.MeatSeafood, chicken.Category);
        var salt = Assert.Single(draft.Ingredients, i => i.Name == "salt");
        Assert.True(salt.IsStaple);
        Assert.Null(salt.Quantity);
        Assert.Empty(await client.GetAs<List<Pantrio.Api.Features.Recipes.RecipeSummaryDto>>("/api/recipes"));
    }

    [Fact]
    public async Task More_mode_returns_only_missing_items()
    {
        var client = await factory.SignedIn();
        var already = new[] { "chicken thigh fillets", "brown onion", "carrot", "potato", "japanese curry roux" };

        var draft = await (await client.PostJson("/api/ai/ingredient-suggestions", Ask(mode: "more", already: already))).ReadAs<IngredientSuggestionResponse>();

        Assert.NotEmpty(draft.Ingredients);
        Assert.DoesNotContain(draft.Ingredients, i => already.Contains(i.Name));
    }

    [Fact]
    public async Task Daily_cap_per_household_is_enforced()
    {
        var client = await factory.SignedIn();
        for (var i = 0; i < PantrioApiFactory.AiDailyLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostJson("/api/ai/ingredient-suggestions", Ask())).StatusCode);
        }

        var res = await client.PostJson("/api/ai/ingredient-suggestions", Ask());
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);

        // Another household is unaffected.
        var other = await factory.SignedIn();
        Assert.Equal(HttpStatusCode.OK, (await other.PostJson("/api/ai/ingredient-suggestions", Ask())).StatusCode);
    }

    [Fact]
    public async Task Requires_sign_in()
    {
        var res = await factory.Anonymous().PostJson("/api/ai/ingredient-suggestions", Ask());
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}

/// <summary>A provider that always fails, in its own app instance.</summary>
public sealed class FailingAiFactory : IAsyncLifetime
{
    public PantrioApiFactory Factory { get; } = new();

    public Task InitializeAsync()
    {
        Factory.Replace<IIngredientSuggester>(new ThrowingSuggester());
        return Factory.InitializeAsync();
    }

    public Task DisposeAsync() => Factory.DisposeAsync();

    private sealed class ThrowingSuggester : IIngredientSuggester
    {
        public string Model => "broken";

        public Task<SuggestionResult> SuggestAsync(SuggestionRequest request, CancellationToken ct) =>
            throw new SuggestionFailedException("nope");
    }
}

public class AiFailureTests(FailingAiFactory fixture) : IClassFixture<FailingAiFactory>
{
    [Fact]
    public async Task Provider_failure_is_a_clear_error_the_ui_can_show()
    {
        var client = await fixture.Factory.SignedIn();

        var res = await client.PostJson("/api/ai/ingredient-suggestions", new { dishName = "Toast", servings = 1 });

        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        Assert.Contains("add ingredients by hand", await res.Content.ReadAsStringAsync());
    }
}

/// <summary>Runs only when PANTRIO_REAL_AI_KEY is set. Calls the real provider once; costs a fraction of a cent.</summary>
public sealed class RealAiFactAttribute : FactAttribute
{
    public RealAiFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PANTRIO_REAL_AI_KEY")))
        {
            Skip = "Set PANTRIO_REAL_AI_KEY (and optionally PANTRIO_REAL_AI_MODEL) to call the real provider.";
        }
    }
}

public class RealAiTests
{
    [RealAiFact]
    public async Task Real_provider_returns_a_valid_draft()
    {
        var options = Options.Create(new AiOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("PANTRIO_REAL_AI_KEY"),
            Model = Environment.GetEnvironmentVariable("PANTRIO_REAL_AI_MODEL") is { Length: > 0 } m ? m : new AiOptions().Model,
        });
        var suggester = new OpenAiIngredientSuggester(options, NullLogger<OpenAiIngredientSuggester>.Instance);

        var result = await suggester.SuggestAsync(
            new SuggestionRequest("Chicken katsu curry", 2, "no coriander", [], [], MoreOnly: false), CancellationToken.None);

        Assert.InRange(result.Ingredients.Count, 5, SuggestionParser.MaxIngredients);
        Assert.Contains(result.Ingredients, i => i.Name.Contains("chicken", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.InputTokens > 0);
    }
}
