using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pantrio.Infrastructure.Ai;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPantrioInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Pantrio")
            ?? "Host=localhost;Port=5433;Database=pantrio;Username=pantrio;Password=pantrio";

        // Registered first so migrations run before any other hosted service (data protection reads its key table at startup).
        services.AddHostedService<MigrationHostedService>();
        services.AddScoped<HouseholdContext>();
        services.AddDbContext<PantrioDbContext>(o => o
            .UseNpgsql(connectionString, npgsql => npgsql
                .MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());

        // Build-time OpenAPI generation runs the app without a database; keys are not needed there.
        var generatingDocument = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
        var dataProtection = services.AddDataProtection().SetApplicationName("pantrio");
        if (!generatingDocument)
        {
            dataProtection.PersistKeysToDbContext<PantrioDbContext>();
        }

        services.Configure<AiOptions>(o =>
        {
            config.GetSection("Ai").Bind(o);
            var openAi = config.GetSection("Ai:OpenAI");
            o.ApiKey = openAi["ApiKey"];
            if (!string.IsNullOrWhiteSpace(openAi["Model"])) o.Model = openAi["Model"]!;
            if (openAi["ReasoningEffort"] is { } effort) o.ReasoningEffort = effort;
        });

        // No key (tests, local dev) means the free deterministic fake; the real provider needs Ai__OpenAI__ApiKey.
        if (string.IsNullOrWhiteSpace(config["Ai:OpenAI:ApiKey"]))
        {
            services.AddSingleton<IIngredientSuggester, FakeIngredientSuggester>();
        }
        else
        {
            services.AddSingleton<IIngredientSuggester, OpenAiIngredientSuggester>();
        }

        return services;
    }
}

/// <summary>Applies pending migrations before the server starts listening (single-instance deployment).</summary>
internal sealed class MigrationHostedService(IServiceProvider services, IConfiguration config, ILogger<MigrationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!config.GetValue("Database:MigrateOnStartup", true))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PantrioDbContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
