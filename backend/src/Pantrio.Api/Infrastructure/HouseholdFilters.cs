using Pantrio.Domain;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Infrastructure;

/// <summary>Role checks, applied per endpoint group. Only the owner role is exercised in Stage 1, but every write is gated.</summary>
public static class HouseholdFilters
{
    /// <summary>Signed in and a member of a household, with at least <paramref name="minimum"/>.</summary>
    public static TBuilder RequireMember<TBuilder>(this TBuilder builder, HouseholdRole minimum = HouseholdRole.Viewer)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var household = ctx.HttpContext.RequestServices.GetRequiredService<HouseholdContext>();
            if (!household.IsSet)
            {
                return TypedResults.Unauthorized();
            }

            if (!household.HasAtLeast(minimum))
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not allowed",
                    detail: $"This needs the {minimum.ToString().ToLowerInvariant()} role.");
            }

            return await next(ctx);
        });

    /// <summary>Deleting recipes and (later) inventory needs the owner role or an editor with the can-delete grant.</summary>
    public static TBuilder RequireCanDelete<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var household = ctx.HttpContext.RequestServices.GetRequiredService<HouseholdContext>();
            if (!household.IsSet)
            {
                return TypedResults.Unauthorized();
            }

            if (!household.MayDelete)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not allowed",
                    detail: "Deleting needs the owner role or the can-delete permission.");
            }

            return await next(ctx);
        });
}
