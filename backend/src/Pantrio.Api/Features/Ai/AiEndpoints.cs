using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Ai;
using Pantrio.Infrastructure.Data;
using Pantrio.Infrastructure.Seed;

namespace Pantrio.Api.Features.Ai;

public enum SuggestionMode
{
    All,
    More,
}

public sealed record IngredientSuggestionRequest(
    [property: Required, StringLength(200, MinimumLength = 1)] string DishName,
    [property: Range(1, 50)] int Servings,
    [property: StringLength(500)] string? Notes,
    SuggestionMode? Mode,
    [property: MaxLength(100)] IReadOnlyList<string>? AlreadyListed);

public sealed record CatalogueMatch(Guid IngredientId, string Name);

public sealed record SuggestedIngredientDto(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? QuantityText,
    AisleCategory Category,
    bool Optional,
    bool IsStaple,
    string? Note,
    /// <summary>Set when the name matched the catalogue exactly or by alias; the row links to that entry.</summary>
    Guid? IngredientId,
    /// <summary>A close-but-not-certain catalogue entry the person may choose to use instead.</summary>
    CatalogueMatch? PossibleMatch);

public sealed record IngredientSuggestionResponse(int Servings, IReadOnlyList<SuggestedIngredientDto> Ingredients, bool IsFake);

public static class AiEndpoints
{
    public static RouteGroupBuilder MapAiEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/ai/ingredient-suggestions", Suggest)
            .WithName("SuggestIngredients")
            .WithTags("AI")
            .RequireMember(HouseholdRole.Editor)
            .RequireRateLimiting(RateLimits.Ai);
        return api;
    }

    private static async Task<Results<Ok<IngredientSuggestionResponse>, ProblemHttpResult>> Suggest(
        IngredientSuggestionRequest body,
        PantrioDbContext db,
        IIngredientSuggester suggester,
        IngredientMatcher matcher,
        IOptions<AiOptions> options,
        ILogger<IngredientSuggestionRequest> logger,
        CancellationToken ct)
    {
        var ctx = db.Household;
        var cfg = options.Value;
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        // Daily caps stop a runaway client (or a stranger who signed up) from spending AI credit.
        if (await db.AiUsage.CountAsync(u => u.CreatedAt >= since, ct) >= cfg.DailyLimitPerHousehold
            || await db.AiUsage.IgnoreQueryFilters().CountAsync(u => u.CreatedAt >= since, ct) >= cfg.DailyLimitGlobal)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Daily suggestion limit reached",
                detail: "You can still add ingredients by hand. Suggestions will be available again tomorrow.");
        }

        var household = await db.Households.AsNoTracking().SingleAsync(h => h.Id == ctx.RequiredHouseholdId, ct);
        var request = new SuggestionRequest(
            body.DishName.Trim(),
            body.Servings,
            string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
            household.DietaryPrefs,
            (body.AlreadyListed ?? []).Select(n => n.Trim()).Where(n => n.Length is > 0 and <= 120).Take(100).ToList(),
            body.Mode == SuggestionMode.More);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(cfg.TimeoutSeconds));
        var usage = new AiUsage { UserId = ctx.RequiredUserId, Purpose = "ingredient_suggestions", Model = suggester.Model };
        SuggestionResult result;
        try
        {
            result = await suggester.SuggestAsync(request, timeout.Token);
            usage.InputTokens = result.InputTokens;
            usage.OutputTokens = result.OutputTokens;
            usage.Succeeded = true;
        }
        catch (Exception ex) when (ex is SuggestionFailedException or OperationCanceledException or System.ClientModel.ClientResultException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ingredient suggestion failed for household {HouseholdId}", ctx.HouseholdId);
            db.AiUsage.Add(usage);
            await db.SaveChangesAsync(CancellationToken.None);
            var timedOut = ex is OperationCanceledException;
            return TypedResults.Problem(
                statusCode: timedOut ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status502BadGateway,
                title: "Couldn't suggest ingredients",
                detail: timedOut
                    ? "The suggestion service took too long. Please try again, or add ingredients by hand."
                    : "The suggestion service didn't return a usable list. Please try again, or add ingredients by hand.");
        }

        db.AiUsage.Add(usage);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AI {Purpose} model={Model} in={InputTokens} out={OutputTokens} household={HouseholdId}",
            usage.Purpose, usage.Model, usage.InputTokens, usage.OutputTokens, ctx.HouseholdId);

        var matches = await matcher.MatchAsync(result.Ingredients.Select(i => i.Name), ct);
        var rows = result.Ingredients.Select(i =>
        {
            matches.TryGetValue(Ingredient.Normalize(i.Name), out var match);
            var linked = match is { Kind: not MatchKind.Fuzzy } ? match.Ingredient : null;
            return new SuggestedIngredientDto(
                linked?.Name ?? i.Name,
                i.Quantity,
                i.Unit,
                i.QuantityText,
                linked?.Category ?? Categories.Parse(i.Category),
                i.Optional,
                linked?.IsStaple ?? i.IsStaple,
                i.Note,
                linked?.Id,
                match is { Kind: MatchKind.Fuzzy } ? new CatalogueMatch(match.Ingredient.Id, match.Ingredient.Name) : null);
        }).ToList();

        return TypedResults.Ok(new IngredientSuggestionResponse(result.Servings, rows, suggester is FakeIngredientSuggester));
    }
}
