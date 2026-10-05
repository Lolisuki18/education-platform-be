using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeds
{
    public static class AdminSeeder
    {
        /// <summary>Password the old seeder gave the administrator on every installation.</summary>
        public const string LegacyDefaultPassword = "18102004";

        /// <summary>Creates the first administrator, once, from configuration. Never invents a password.</summary>
        public static async Task SeedAsync(EducationPlatformDBContext context, AdminSeedOptions admin, ILogger logger)
        {
            if (await context.Users.AnyAsync(u => u.Role == Role.Admin))
                return;

            if (!admin.IsConfigured)
            {
                logger.LogWarning(
                    "No administrator exists. Set Admin:Email and Admin:Password (e.g. Admin__Email / Admin__Password) to create the first one.");
                return;
            }

            context.Users.Add(new User(
                Guid.NewGuid(),
                admin.Email!.Trim(),
                admin.Password!,
                admin.Phone,
                admin.Name,
                "Platform Administrator",
                Role.Admin,
                null,
                true));

            logger.LogInformation("Created the first administrator account {Email}.", Application.Common.LogMask.Email(admin.Email));
        }

        /// <summary>
        /// Databases seeded by earlier versions contain an administrator with a password that is public knowledge
        /// (it was in the source code). Shout about it on every start until somebody changes it.
        /// </summary>
        public static async Task WarnAboutLegacyPasswordsAsync(EducationPlatformDBContext context, ILogger logger)
        {
            var admins = await context.Users.AsNoTracking().Where(u => u.Role == Role.Admin).ToListAsync();

            foreach (var admin in admins)
            {
                if (admin.Password.Verify(LegacyDefaultPassword))
                {
                    logger.LogCritical(
                        "Administrator {Email} still uses the default password that shipped with earlier versions. Change it now.",
                        Application.Common.LogMask.Email(admin.Email));
                }
            }
        }
    }
}
