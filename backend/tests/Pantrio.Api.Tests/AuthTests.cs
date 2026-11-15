using System.Net;
using System.Net.Http.Json;
using Pantrio.Api.Features.Households;
using Pantrio.Api.Tests.Infrastructure;
using Pantrio.Domain;

namespace Pantrio.Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthTests(PantrioApiFactory factory)
{
    [Fact]
    public async Task Health_is_public()
    {
        var res = await factory.Anonymous().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Me_requires_sign_in()
    {
        var res = await factory.Anonymous().GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Signing_up_creates_a_household_with_the_user_as_owner()
    {
        var client = await factory.SignedIn("katsu.fan@example.test");

        var me = await client.GetAs<MeResponse>("/api/me");

        Assert.Equal("katsu.fan@example.test", me.User.Email);
        Assert.Equal(HouseholdRole.Owner, me.Role);
        Assert.True(me.CanDelete);
        Assert.Equal("Australia/Sydney", me.Household.TimeZone);
        Assert.Equal("Test's household", me.Household.Name);
    }

    [Fact]
    public async Task Signing_in_again_reuses_the_household()
    {
        var email = $"again-{Guid.NewGuid():N}@example.test";
        var first = await (await factory.SignedIn(email)).GetAs<MeResponse>("/api/me");
        var second = await (await factory.SignedIn(email)).GetAs<MeResponse>("/api/me");

        Assert.Equal(first.User.Id, second.User.Id);
        Assert.Equal(first.Household.Id, second.Household.Id);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var client = await factory.SignedIn();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJson("/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Unsafe_requests_without_the_csrf_header_are_refused()
    {
        var client = await factory.SignedIn();
        client.DefaultRequestHeaders.Remove("X-Pantrio-Csrf");

        var res = await client.PatchAsJsonAsync("/api/household", new { name = "Hacked" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Unsafe_requests_from_a_foreign_origin_are_refused()
    {
        var client = await factory.SignedIn();
        using var req = new HttpRequestMessage(HttpMethod.Patch, "/api/household") { Content = JsonContent.Create(new { name = "Hacked" }) };
        req.Headers.Add("Origin", "https://evil.example");

        var res = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Owner_can_update_household_settings()
    {
        var client = await factory.SignedIn();

        var updated = await (await client.PatchJson("/api/household", new
        {
            name = "The Liu household",
            timeZone = "Australia/Melbourne",
            hiddenMealSlots = new[] { MealSlot.Breakfast },
        })).ReadAs<HouseholdDto>();

        Assert.Equal("The Liu household", updated.Name);
        Assert.Equal("Australia/Melbourne", updated.TimeZone);
        Assert.Equal([MealSlot.Breakfast], updated.HiddenMealSlots);
    }

    [Fact]
    public async Task Unknown_time_zone_is_a_validation_error()
    {
        var client = await factory.SignedIn();
        var res = await client.PatchJson("/api/household", new { timeZone = "Mars/Olympus" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Dev_login_validates_its_input()
    {
        var res = await factory.Anonymous().PostJson("/api/auth/dev-login", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Providers_report_dev_login_and_no_google_in_tests()
    {
        var providers = await factory.Anonymous().GetAs<Pantrio.Api.Auth.AuthProvidersResponse>("/api/auth/providers");
        Assert.True(providers.DevLogin);
        Assert.False(providers.Google);
    }

    [Fact]
    public async Task Google_login_is_not_found_when_not_configured()
    {
        var res = await factory.Anonymous().GetAsync("/api/auth/google/login");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
