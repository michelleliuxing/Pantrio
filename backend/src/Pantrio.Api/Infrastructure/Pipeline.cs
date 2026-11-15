using Microsoft.EntityFrameworkCore;
using Pantrio.Api.Auth;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api.Infrastructure;

public static class Pipeline
{
    public const string CsrfHeader = "X-Pantrio-Csrf";

    /// <summary>
    /// The API is only ever reached through the frontend's /api proxy, so URLs it builds (the Google redirect_uri)
    /// must use the public frontend origin rather than whatever host the proxy connected to.
    /// </summary>
    public static IApplicationBuilder UsePublicOrigin(this IApplicationBuilder app, Uri publicOrigin) =>
        app.Use((ctx, next) =>
        {
            ctx.Request.Scheme = publicOrigin.Scheme;
            ctx.Request.Host = new HostString(publicOrigin.Authority);
            return next();
        });

    /// <summary>
    /// CSRF protection for cookie auth: unsafe methods need a custom header (which a cross-site form cannot send
    /// without a CORS preflight the API never approves), and a browser-sent Origin must be our own.
    /// </summary>
    public static IApplicationBuilder UseCsrfProtection(this IApplicationBuilder app, Uri publicOrigin) =>
        app.Use(async (ctx, next) =>
        {
            if (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method))
            {
                await next();
                return;
            }

            var origin = ctx.Request.Headers.Origin.ToString();
            var originOk = origin.Length == 0 || string.Equals(origin.TrimEnd('/'), publicOrigin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
            if (!ctx.Request.Headers.ContainsKey(CsrfHeader) || !originOk)
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Request blocked",
                    detail: "Missing CSRF header or foreign origin.").ExecuteAsync(ctx);
                return;
            }

            await next();
        });

    /// <summary>Resolves the signed-in user's household once per request; the DbContext filters by it.</summary>
    public static IApplicationBuilder UseHouseholdContext(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            if (ctx.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(ctx.User.FindFirst(AccountProvisioning.UserIdClaim)?.Value, out var userId))
            {
                var db = ctx.RequestServices.GetRequiredService<PantrioDbContext>();
                var member = await db.HouseholdMembers.AsNoTracking()
                    .Where(m => m.UserId == userId)
                    .OrderBy(m => m.JoinedAt)
                    .FirstOrDefaultAsync(ctx.RequestAborted);
                if (member is not null)
                {
                    ctx.RequestServices.GetRequiredService<HouseholdContext>()
                        .Set(member.HouseholdId, userId, member.Role, member.CanDelete);
                }
            }

            await next();
        });

    /// <summary>Client IP for rate limiting. Behind Vercel and Caddy the first X-Forwarded-For entry is the client.</summary>
    public static string ClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (forwarded.Length > 0)
        {
            return forwarded.Split(',')[0].Trim();
        }

        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
