using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pantrio.Infrastructure.Data;

namespace Pantrio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPantrioInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Pantrio")
            ?? "Host=localhost;Port=5433;Database=pantrio;Username=pantrio;Password=pantrio";

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

        services.AddHostedService<MigrationHostedService>();
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
