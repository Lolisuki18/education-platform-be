using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Respawn;
using Xunit;

using Infrastructure.Persistence.Seeds;

namespace IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private Respawner? _respawner;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:Server", "Host=localhost;Database=EducationPlatformDB_Test;Username=postgres;Password=12345" },
                    { "Storage:RootPath", Path.Combine(Directory.GetCurrentDirectory(), "test_storage") },
                    { "EmailSettings:SmtpHost", "localhost" },
                    { "EmailSettings:SmtpPort", "25" }
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // 1. Override DB Connection to Test Database
                var dbContextOptionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<EducationPlatformDBContext>));
                if (dbContextOptionsDescriptor != null)
                {
                    services.Remove(dbContextOptionsDescriptor);
                }

                services.AddDbContext<EducationPlatformDBContext>((sp, options) =>
                {
                    var interceptor = sp.GetRequiredService<Infrastructure.Persistence.Interceptors.DomainEventDispatcherInterceptor>();
                    options.UseNpgsql("Host=localhost;Database=EducationPlatformDB_Test;Username=postgres;Password=12345")
                           .AddInterceptors(interceptor)
                           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                });

                var appDbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(Application.Interface.IApplicationDBContext));
                if (appDbContextDescriptor != null)
                {
                    services.Remove(appDbContextDescriptor);
                }
                services.AddScoped<Application.Interface.IApplicationDBContext>(sp => sp.GetRequiredService<EducationPlatformDBContext>());

                // 2. Remove existing payment service registration if any, and add mock
                var paymentDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Application.Interface.IPaymentService));
                if (paymentDescriptor != null)
                {
                    services.Remove(paymentDescriptor);
                }

                var mockPaymentService = new Mock<Application.Interface.IPaymentService>();
                mockPaymentService
                    .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>()))
                    .ReturnsAsync("https://mock-payment-url.com");

                services.AddScoped(_ => mockPaymentService.Object);
            });
        }

        public async Task InitializeAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();

            // Delete and migrate test database on suite startup
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = new[] { "public" },
                TablesToIgnore = new[] { new Respawn.Graph.Table("__EFMigrationsHistory") }
            });
        }

        public async Task ResetDatabaseAsync()
        {
            if (_respawner != null)
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                var connection = db.Database.GetDbConnection();
                await connection.OpenAsync();

                await _respawner.ResetAsync(connection);

                // Re-seed lightweight data for testing after tables are reset
                await TestSeeder.SeedAsync(db);
            }
        }

        public new Task DisposeAsync()
        {
            var testStorage = Path.Combine(Directory.GetCurrentDirectory(), "test_storage");
            if (Directory.Exists(testStorage))
            {
                try
                {
                    Directory.Delete(testStorage, true);
                }
                catch
                {
                    // Ignore deletion exceptions in cleanup
                }
            }
            return Task.CompletedTask;
        }
    }
}
