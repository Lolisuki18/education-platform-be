using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence
{
    public class EducationPlatformDBContextFactory
        : IDesignTimeDbContextFactory<EducationPlatformDBContext>
    {
        public EducationPlatformDBContext CreateDbContext(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Get connection string
            var connectionString = configuration.GetConnectionString("Default") ?? configuration.GetConnectionString("Server");

            // Build DbContext
            var optionsBuilder = new DbContextOptionsBuilder<EducationPlatformDBContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new EducationPlatformDBContext(optionsBuilder.Options);
        }
    }
}

