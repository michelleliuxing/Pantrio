using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Infrastructure;
using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Features.Households;

public sealed record UserDto(Guid Id, string Email, string DisplayName, string? AvatarUrl);

public sealed record HouseholdDto(Guid Id, string Name, string TimeZone, IReadOnlyList<MealSlot> HiddenMealSlots, IReadOnlyList<string> DietaryPrefs);

public sealed record MeResponse(UserDto User, HouseholdDto Household, HouseholdRole Role, bool CanDelete);

public sealed record UpdateHouseholdRequest(
    [property: StringLength(100, MinimumLength = 1)] string? Name,
    [property: StringLength(64)] string? TimeZone,
    IReadOnlyList<MealSlot>? HiddenMealSlots,
    [property: MaxLength(20)] IReadOnlyList<string>? DietaryPrefs);

public static class HouseholdEndpoints
{
    public static RouteGroupBuilder MapHouseholdEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> (PantrioDbContext db, CancellationToken ct) =>
            {
                var ctx = db.Household;
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ctx.RequiredUserId, ct);
                var home = await db.Households.AsNoTracking().SingleAsync(h => h.Id == ctx.RequiredHouseholdId, ct);
                return TypedResults.Ok(new MeResponse(
                    new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl),
                    ToDto(home), ctx.Role!.Value, ctx.MayDelete));
            })
            .WithName("GetMe")
            .WithTags("Household")
            .RequireMember();

        api.MapPatch("/household", async Task<Results<Ok<HouseholdDto>, ValidationProblem>> (UpdateHouseholdRequest body, PantrioDbContext db, CancellationToken ct) =>
            {
                var home = await db.Households.SingleAsync(h => h.Id == db.Household.RequiredHouseholdId, ct);
                if (body.TimeZone is { } tz && !TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
                {
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["timeZone"] = ["Unknown time zone."] });
                }

                if (body.Name is { } name) home.Name = name.Trim();
                if (body.TimeZone is { } zone) home.TimeZone = zone;
                if (body.HiddenMealSlots is { } hidden) home.HiddenMealSlots = hidden.Distinct().Order().ToList();
                if (body.DietaryPrefs is { } prefs)
                {
                    home.DietaryPrefs = prefs.Select(p => p.Trim()).Where(p => p.Length is > 0 and <= 60)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }

                await db.SaveChangesAsync(ct);
                return TypedResults.Ok(ToDto(home));
            })
            .WithName("UpdateHousehold")
            .WithTags("Household")
            .RequireMember(HouseholdRole.Owner);

        return api;
    }

    private static HouseholdDto ToDto(Household h) => new(h.Id, h.Name, h.TimeZone, h.HiddenMealSlots, h.DietaryPrefs);
}
