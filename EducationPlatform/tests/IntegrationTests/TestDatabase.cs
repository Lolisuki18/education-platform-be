using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace IntegrationTests
{
    /// <summary>
    /// Finds a PostgreSQL database for the integration tests, in this order:
    /// <list type="number">
    /// <item><c>TEST_DB_CONNECTION_STRING</c> (CI service container, or any database you point it at);</item>
    /// <item>a throw-away container started by Testcontainers, when Docker is available (set
    /// <c>TESTCONTAINERS_DISABLED=true</c> to skip this);</item>
    /// <item><c>ConnectionStrings:Test</c> from the test project's appsettings.json (a locally installed PostgreSQL).</item>
    /// </list>
    /// The database is wiped and migrated by the test factory, so never point this at data you care about.
    /// </summary>
    public static class TestDatabase
    {
        private static readonly Lazy<string> Resolved = new(Resolve, LazyThreadSafetyMode.ExecutionAndPublication);

        private static PostgreSqlContainer? _container;

        public static string ConnectionString => Resolved.Value;

        private static string Resolve()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;

            if (!string.Equals(Environment.GetEnvironmentVariable("TESTCONTAINERS_DISABLED"), "true", StringComparison.OrdinalIgnoreCase))
            {
                var container = TryStartContainer();
                if (container != null)
                    return container.GetConnectionString();
            }

            return new ConfigurationBuilder()
                       .SetBasePath(Directory.GetCurrentDirectory())
                       .AddJsonFile("appsettings.json", optional: false)
                       .Build()
                       .GetConnectionString("Test")
                   ?? throw new InvalidOperationException(
                       "No test database available. Start Docker (a PostgreSQL container is created automatically), " +
                       "set TEST_DB_CONNECTION_STRING, or set ConnectionStrings:Test in the test project's appsettings.json.");
        }

        private static PostgreSqlContainer? TryStartContainer()
        {
            try
            {
                var container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .WithDatabase("EducationPlatformDB_Test")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

                container.StartAsync().GetAwaiter().GetResult();

                _container = container;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => _container?.DisposeAsync().AsTask().GetAwaiter().GetResult();

                return container;
            }
            catch (Exception ex)
            {
                // Docker is not installed or not running: fall back to a locally installed database
                Console.Error.WriteLine($"[IntegrationTests] Testcontainers unavailable ({ex.GetType().Name}), falling back to the configured database.");
                return null;
            }
        }
    }
}
