using System.Threading.RateLimiting;
using Pantrio.Api.Infrastructure;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Api;

public static class RateLimits
{
    public const string Auth = "auth";
    public const string Ai = "ai";

    public static IServiceCollection AddPantrioRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 20);
        var aiPerMinute = config.GetValue("RateLimits:AiPerMinute", 6);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests",
                    detail: "Please wait a moment and try again.").ExecuteAsync(ctx.HttpContext);
            };

            o.AddPolicy(Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(Pipeline.ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));

            o.AddPolicy(Ai, ctx =>
            {
                var household = ctx.RequestServices.GetRequiredService<HouseholdContext>();
                var key = household.HouseholdId?.ToString() ?? Pipeline.ClientIp(ctx);
                return RateLimitPartition.GetFixedWindowLimiter(key,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = aiPerMinute, Window = TimeSpan.FromMinutes(1) });
            });
        });

        return services;
    }
}
