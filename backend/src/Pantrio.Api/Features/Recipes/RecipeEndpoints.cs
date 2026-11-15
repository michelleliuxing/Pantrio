using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Features.Ingredients;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Recipes;

public static class RecipeEndpoints
{
    public static RouteGroupBuilder MapRecipeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/recipes").WithTags("Recipes").RequireMember();

        group.MapGet("/", async (string? search, string? tag, bool? favourites, RecipeSort? sort, PantrioDbContext db, CancellationToken ct) =>
            {
                var query = db.Recipes.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var pattern = $"%{IngredientEndpoints.EscapeLike(search.Trim())}%";
                    query = query.Where(r => EF.Functions.ILike(r.Name, pattern) || r.Tags.Any(t => EF.Functions.ILike(t, pattern)));
                }

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    var t = tag.Trim().ToLowerInvariant();
                    query = query.Where(r => r.Tags.Contains(t));
                }

                if (favourites == true)
                {
                    query = query.Where(r => r.IsFavourite);
                }

                query = sort == RecipeSort.Recent
                    ? query.OrderByDescending(r => r.UpdatedAt)
                    : query.OrderByDescending(r => r.IsFavourite).ThenBy(r => r.Name);

                var items = await query
                    .Select(r => new RecipeSummaryDto(r.Id, r.Name, r.Servings, r.Tags, r.PrepMins, r.IsFavourite, r.Ingredients.Count, r.UpdatedAt))
                    .ToListAsync(ct);
                return TypedResults.Ok(items);
            })
            .WithName("ListRecipes");

        group.MapGet("/tags", async (PantrioDbContext db, CancellationToken ct) =>
            {
                var tags = await db.Recipes.AsNoTracking().SelectMany(r => r.Tags).ToListAsync(ct);
                return TypedResults.Ok(tags.GroupBy(t => t).Select(g => new TagCount(g.Key, g.Count()))
                    .OrderByDescending(t => t.Count).ThenBy(t => t.Tag).ToList());
            })
            .WithName("ListRecipeTags");

        group.MapGet("/{id:guid}", async Task<Results<Ok<RecipeDto>, NotFound>> (Guid id, RecipeService recipes, CancellationToken ct) =>
                await recipes.LoadAsync(id, ct) is { } recipe ? TypedResults.Ok(RecipeDto.From(recipe)) : TypedResults.NotFound())
            .WithName("GetRecipe");

        group.MapPost("/", async Task<Results<Created<RecipeDto>, ValidationProblem>> (
                RecipeUpsertRequest body, RecipeService recipes, PantrioDbContext db, CancellationToken ct) =>
            {
                var errors = RecipeService.Validate(body);
                if (errors.Count > 0)
                {
                    return RecipeService.Problem(errors);
                }

                var recipe = new Recipe { Name = body.Name, CreatedBy = db.Household.RequiredUserId };
                db.Recipes.Add(recipe);
                await recipes.ApplyAsync(recipe, body, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Created($"/api/recipes/{recipe.Id}", RecipeDto.From(recipe));
            })
            .WithName("CreateRecipe")
            .RequireMember(HouseholdRole.Editor);

        group.MapPut("/{id:guid}", async Task<Results<Ok<RecipeDto>, NotFound, ValidationProblem>> (
                Guid id, RecipeUpsertRequest body, RecipeService recipes, PantrioDbContext db, CancellationToken ct) =>
            {
                var errors = RecipeService.Validate(body);
                if (errors.Count > 0)
                {
                    return RecipeService.Problem(errors);
                }

                var recipe = await recipes.LoadAsync(id, ct, track: true);
                if (recipe is null)
                {
                    return TypedResults.NotFound();
                }

                await recipes.ApplyAsync(recipe, body, ct);
                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(RecipeDto.From(recipe));
            })
            .WithName("UpdateRecipe")
            .RequireMember(HouseholdRole.Editor);

        group.MapPatch("/{id:guid}", async Task<Results<Ok<RecipeSummaryDto>, NotFound>> (
                Guid id, UpdateRecipeFlagsRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (recipe is null)
                {
                    return TypedResults.NotFound();
                }

                if (body.IsFavourite is { } fav) recipe.IsFavourite = fav;
                await db.SaveChangesAsync(ct);
                var count = await db.RecipeIngredients.CountAsync(i => i.RecipeId == id, ct);
                return TypedResults.Ok(new RecipeSummaryDto(recipe.Id, recipe.Name, recipe.Servings, recipe.Tags, recipe.PrepMins,
                    recipe.IsFavourite, count, recipe.UpdatedAt));
            })
            .WithName("UpdateRecipeFlags")
            .RequireMember(HouseholdRole.Editor);

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, PantrioDbContext db, CancellationToken ct) =>
            {
                var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct);
                if (recipe is null)
                {
                    return TypedResults.NotFound();
                }

                db.Recipes.Remove(recipe);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteRecipe")
            .RequireCanDelete();

        group.MapPost("/{id:guid}/duplicate", async Task<Results<Created<RecipeDto>, NotFound>> (
                Guid id, RecipeService recipes, PantrioDbContext db, CancellationToken ct) =>
            {
                var source = await recipes.LoadAsync(id, ct);
                if (source is null)
                {
                    return TypedResults.NotFound();
                }

                var copy = new Recipe
                {
                    Name = $"{source.Name} (copy)",
                    Servings = source.Servings,
                    Tags = [.. source.Tags],
                    PrepMins = source.PrepMins,
                    Notes = source.Notes,
                    MethodText = source.MethodText,
                    MethodSource = source.MethodSource,
                    SourceUrl = source.SourceUrl,
                    CreatedBy = db.Household.RequiredUserId,
                };
                foreach (var i in source.Ingredients.OrderBy(i => i.SortOrder))
                {
                    copy.Ingredients.Add(new RecipeIngredient
                    {
                        RecipeId = copy.Id,
                        IngredientId = i.IngredientId,
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        QuantityText = i.QuantityText,
                        Optional = i.Optional,
                        Note = i.Note,
                        SortOrder = i.SortOrder,
                    });
                }

                db.Recipes.Add(copy);
                await db.SaveChangesAsync(ct);
                var saved = (await recipes.LoadAsync(copy.Id, ct))!;
                return TypedResults.Created($"/api/recipes/{copy.Id}", RecipeDto.From(saved));
            })
            .WithName("DuplicateRecipe")
            .RequireMember(HouseholdRole.Editor);

        return api;
    }
}
