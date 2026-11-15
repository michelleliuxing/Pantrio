using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Pantrio.Api.Tests.Infrastructure;

/// <summary>Runs the real API against a real PostgreSQL in a container. One database for the whole test run;
/// each test signs in as fresh users, so every test gets its own households.</summary>
public sealed class PantrioApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public static readonly JsonSerializerOptions Json = CreateJson();

    public Action<IServiceCollection>? ConfigureTestServices { get; set; }

    public string Environment { get; init; } = "Testing";

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // start the host (runs migrations) once the database is up
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        builder.UseSetting("ConnectionStrings:Pantrio", _db.GetConnectionString());
        builder.UseSetting("App:PublicOrigin", "http://localhost");
        builder.UseSetting("Auth:DevLogin:Enabled", "true");
        builder.UseSetting("RateLimits:AuthPerMinute", "10000");
        builder.UseSetting("RateLimits:AiPerMinute", "10000");
        builder.UseSetting("Ai:OpenAI:ApiKey", "");
        builder.ConfigureServices(services => ConfigureTestServices?.Invoke(services));
    }

    /// <summary>A client with its own cookie jar and the CSRF header every browser request carries.</summary>
    public HttpClient Anonymous()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Pantrio-Csrf", "1");
        return client;
    }

    /// <summary>Signs in through the dev login. A new email creates a new household.</summary>
    public async Task<HttpClient> SignedIn(string? email = null)
    {
        var client = Anonymous();
        email ??= $"user-{Guid.NewGuid():N}@example.test";
        var res = await client.PostAsJsonAsync("/api/auth/dev-login", new { email, displayName = "Test Person" });
        res.EnsureSuccessStatusCode();
        return client;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return o;
    }

    public void Replace<TService>(TService implementation) where TService : class =>
        ConfigureTestServices += s =>
        {
            s.RemoveAll<TService>();
            s.AddSingleton(implementation);
        };
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PantrioApiFactory>
{
    public const string Name = "api";
}

public static class HttpExtensions
{
    public static async Task<T> ReadAs<T>(this HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} {res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<T>(PantrioApiFactory.Json))!;
    }

    public static async Task<T> GetAs<T>(this HttpClient client, string url) => await (await client.GetAsync(url)).ReadAs<T>();

    public static Task<HttpResponseMessage> PostJson(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, PantrioApiFactory.Json);

    public static Task<HttpResponseMessage> PutJson(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, PantrioApiFactory.Json);

    public static Task<HttpResponseMessage> PatchJson(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, PantrioApiFactory.Json);
}
