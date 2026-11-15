using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace Pantrio.Api.Auth;

public static class AuthSetup
{
    public const string AppCookie = "pantrio";
    public const string ExternalCookie = "pantrio.external";

    public static bool GoogleConfigured(IConfiguration config) =>
        !string.IsNullOrWhiteSpace(config["Auth:Google:ClientId"]) && !string.IsNullOrWhiteSpace(config["Auth:Google:ClientSecret"]);

    /// <summary>The dev-only sign-in. Never available in Production, whatever the configuration says.</summary>
    public static bool DevLoginEnabled(IConfiguration config, IHostEnvironment env) =>
        !env.IsProduction() && config.GetValue("Auth:DevLogin:Enabled", false);

    public static IServiceCollection AddPantrioAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var auth = services
            .AddAuthentication(AppCookie)
            .AddCookie(AppCookie, o =>
            {
                o.Cookie.Name = "pantrio.auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = env.IsDevelopment() || env.IsEnvironment("Testing")
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromDays(30);
                o.SlidingExpiration = true;

                // An API answers with status codes, never with redirects to a login page.
                o.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                o.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddCookie(ExternalCookie, o =>
            {
                o.Cookie.Name = "pantrio.external";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        if (GoogleConfigured(config))
        {
            auth.AddGoogle(GoogleDefaults.AuthenticationScheme, o =>
            {
                o.ClientId = config["Auth:Google:ClientId"]!;
                o.ClientSecret = config["Auth:Google:ClientSecret"]!;
                o.SignInScheme = ExternalCookie;
                o.CallbackPath = "/api/auth/google/callback";
                o.ClaimActions.MapJsonKey("picture", "picture");
                // The callback is a top-level GET redirect from Google, so Lax cookies are sent; None would need HTTPS in dev.
                o.CorrelationCookie.SameSite = SameSiteMode.Lax;
                o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.Events.OnRemoteFailure = ctx =>
                {
                    ctx.HandleResponse();
                    ctx.Response.Redirect("/sign-in?error=google");
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();
        services.AddScoped<AccountProvisioning>();
        return services;
    }

    /// <summary>Only same-site relative paths may be used as a post-login destination.</summary>
    public static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal) && !returnUrl.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : "/plan";

    public static async Task SignInAsync(HttpContext http, System.Security.Claims.ClaimsPrincipal principal) =>
        await http.SignInAsync(AppCookie, principal, new AuthenticationProperties { IsPersistent = true });
}
