using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Pantrio.Api.Auth;

public sealed record AuthProvidersResponse(bool Google, bool DevLogin);

public sealed record DevLoginRequest(
    [property: Required, EmailAddress, StringLength(320)] string Email,
    [property: StringLength(200)] string? DisplayName);

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapGet("/providers", (IConfiguration config, IHostEnvironment env) =>
                TypedResults.Ok(new AuthProvidersResponse(AuthSetup.GoogleConfigured(config), AuthSetup.DevLoginEnabled(config, env))))
            .WithName("GetAuthProviders");

        auth.MapGet("/google/login", Results<ChallengeHttpResult, ProblemHttpResult> (string? returnUrl, IConfiguration config) =>
            {
                if (!AuthSetup.GoogleConfigured(config))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Google sign-in is not configured");
                }

                var complete = $"/api/auth/google/complete?returnUrl={Uri.EscapeDataString(AuthSetup.SafeReturnUrl(returnUrl))}";
                return TypedResults.Challenge(new AuthenticationProperties { RedirectUri = complete }, [GoogleDefaults.AuthenticationScheme]);
            })
            .WithName("GoogleLogin")
            .RequireRateLimiting(RateLimits.Auth);

        auth.MapGet("/google/complete", async Task<RedirectHttpResult> (string? returnUrl, HttpContext http, AccountProvisioning accounts, CancellationToken ct) =>
            {
                var external = await http.AuthenticateAsync(AuthSetup.ExternalCookie);
                var principal = external.Principal;
                var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var email = principal?.FindFirstValue(ClaimTypes.Email);
                if (!external.Succeeded || principal is null || subject is null || email is null)
                {
                    return TypedResults.Redirect("/sign-in?error=google");
                }

                var identity = new ExternalIdentity("google", subject, email,
                    principal.FindFirstValue(ClaimTypes.Name) ?? email, principal.FindFirstValue("picture"));
                var appPrincipal = await accounts.SignInOrSignUpAsync(identity, ct);
                await http.SignOutAsync(AuthSetup.ExternalCookie);
                await AuthSetup.SignInAsync(http, appPrincipal);
                return TypedResults.Redirect(AuthSetup.SafeReturnUrl(returnUrl));
            })
            .WithName("GoogleLoginComplete")
            .ExcludeFromDescription();

        auth.MapPost("/dev-login", async Task<Results<NoContent, NotFound>> (DevLoginRequest body, HttpContext http,
                IConfiguration config, IHostEnvironment env, AccountProvisioning accounts, CancellationToken ct) =>
            {
                if (!AuthSetup.DevLoginEnabled(config, env))
                {
                    return TypedResults.NotFound();
                }

                var email = body.Email.Trim().ToLowerInvariant();
                var name = string.IsNullOrWhiteSpace(body.DisplayName) ? email.Split('@')[0] : body.DisplayName.Trim();
                var principal = await accounts.SignInOrSignUpAsync(new ExternalIdentity("dev", email, email, name, null), ct);
                await AuthSetup.SignInAsync(http, principal);
                return TypedResults.NoContent();
            })
            .WithName("DevLogin")
            .RequireRateLimiting(RateLimits.Auth);

        auth.MapPost("/logout", async Task<NoContent> (HttpContext http) =>
            {
                await http.SignOutAsync(AuthSetup.AppCookie);
                return TypedResults.NoContent();
            })
            .WithName("Logout");

        return api;
    }
}
