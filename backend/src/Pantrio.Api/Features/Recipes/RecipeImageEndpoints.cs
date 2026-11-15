using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Recipes;

public sealed record RecipeImageDto(string ImageUrl);

/// <summary>
/// One photo per recipe. The browser resizes before upload; the server only accepts JPEG, PNG or WebP,
/// identified from the file's own bytes, and serves it back with headers that stop it being run as anything else.
/// </summary>
public static class RecipeImageEndpoints
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static string UrlFor(Guid recipeId, DateTimeOffset updatedAt) =>
        $"/api/recipes/{recipeId}/image?v={updatedAt.UtcTicks}";

    public static RouteGroupBuilder MapRecipeImageEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/recipes/{id:guid}/image").WithTags("Recipes").RequireMember();

        group.MapGet("/", async Task<Results<FileContentHttpResult, NotFound>> (Guid id, string? v, HttpContext http, PantrioDbContext db, CancellationToken ct) =>
            {
                var image = await db.RecipeImages.AsNoTracking().FirstOrDefaultAsync(i => i.RecipeId == id, ct);
                if (image is null) return TypedResults.NotFound();

                var headers = http.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
                // Versioned URLs never change, so the browser may keep them; the URL changes when the photo does.
                headers.CacheControl = v is null ? "private, no-cache" : "private, max-age=31536000, immutable";
                return TypedResults.File(image.Data, image.ContentType,
                    entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{image.UpdatedAt.UtcTicks}\""),
                    lastModified: image.UpdatedAt);
            })
            .WithName("GetRecipeImage")
            .ExcludeFromDescription();

        group.MapPut("/", async Task<Results<Ok<RecipeImageDto>, NotFound, ValidationProblem>> (
                Guid id, IFormFile file, PantrioDbContext db, CancellationToken ct) =>
            {
                if (!await db.Recipes.AnyAsync(r => r.Id == id, ct)) return TypedResults.NotFound();
                if (file.Length is 0 or > MaxBytes)
                {
                    return Problem("Choose a photo up to 2 MB.");
                }

                var data = new byte[file.Length];
                await using (var stream = file.OpenReadStream())
                {
                    await stream.ReadExactlyAsync(data, ct);
                }

                var contentType = DetectImageType(data);
                if (contentType is null)
                {
                    return Problem("Use a JPEG, PNG or WebP photo.");
                }

                var image = await db.RecipeImages.FirstOrDefaultAsync(i => i.RecipeId == id, ct);
                if (image is null)
                {
                    image = new RecipeImage { RecipeId = id, ContentType = contentType, Data = data };
                    db.RecipeImages.Add(image);
                }
                else
                {
                    image.ContentType = contentType;
                    image.Data = data;
                    image.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(new RecipeImageDto(UrlFor(id, image.UpdatedAt)));
            })
            .WithName("UploadRecipeImage")
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery() // cookie auth is protected by the X-Pantrio-Csrf header + Origin check instead
            .WithMetadata(new RequestSizeLimitAttribute(MaxBytes + 64 * 1024))
            .RequireMember(HouseholdRole.Editor);

        group.MapDelete("/", async Task<Results<NoContent, NotFound>> (Guid id, PantrioDbContext db, CancellationToken ct) =>
            {
                var image = await db.RecipeImages.FirstOrDefaultAsync(i => i.RecipeId == id, ct);
                if (image is null) return TypedResults.NotFound();
                db.RecipeImages.Remove(image);
                await db.SaveChangesAsync(ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteRecipeImage")
            .RequireMember(HouseholdRole.Editor);

        return api;
    }

    /// <summary>Identifies JPEG, PNG and WebP by their signatures. Anything else (SVG, HTML, HEIC, PDF) is refused.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    private static ValidationProblem Problem(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [message] });
}
