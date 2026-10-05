using System.Data.Common;
using Infrastructure.Persistence.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence
{
    /// <summary>Applies migrations and seeds reference data, safely even when several instances start together.</summary>
    public static class DatabaseInitializer
    {
        // Arbitrary but fixed: every instance must ask for the same advisory lock
        private const long AdvisoryLockKey = 7426913501;

        /// <summary>
        /// Demo data is opt-in: on by default only in Development, and only when <c>Database:SeedDemoData</c> says so elsewhere.
        /// </summary>
        internal static SeedOptions ReadSeedOptions(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();

            var options = new SeedOptions
            {
                DemoData = configuration.GetValue("Database:SeedDemoData", environment.IsDevelopment()),
                DemoPassword = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? SeedOptions.DefaultDemoPassword
            };

            configuration.GetSection("Admin").Bind(options.Admin);
            return options;
        }

        public static async Task InitializeAsync(
            IServiceProvider services,
            ILogger logger,
            int retries = 5,
            CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    await using var scope = services.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();

                    // The lock belongs to the connection, so keep one connection open for the whole run
                    await db.Database.OpenConnectionAsync(cancellationToken);
                    try
                    {
                        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({AdvisoryLockKey})", cancellationToken);

                        await db.Database.MigrateAsync(cancellationToken);
                        await Seeder.SeedAsync(db, ReadSeedOptions(scope.ServiceProvider), logger);
                    }
                    finally
                    {
                        // Closing the connection releases the lock even if the explicit unlock fails
                        try
                        {
                            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Could not release the migration lock explicitly.");
                        }

                        await db.Database.CloseConnectionAsync();
                    }

                    logger.LogInformation("Database migrated and seeded successfully.");
                    return;
                }
                catch (DbException ex) when (attempt < retries)
                {
                    logger.LogWarning(ex, "Database not ready, retrying in 5s... ({Attempt}/{Retries})", attempt, retries);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
    }
}
