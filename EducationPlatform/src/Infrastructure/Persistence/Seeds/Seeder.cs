using Microsoft.Extensions.Logging;
namespace Infrastructure.Persistence.Seeds
{
    public class UserSeedResult
    {
        public List<Guid> AdminIds { get; set; } = new();
        public List<Guid> TeacherIds { get; set; } = new();
        public List<Guid> StudentIds { get; set; } = new();
    }

    public class CourseSeedResult
    {
        public List<Guid> CourseIds { get; set; } = new();
        public Dictionary<Guid, decimal?> CoursePrices { get; set; } = new();
    }

    public class EnrollmentSeedResult
    {
        public List<Guid> EnrollmentIds { get; set; } = new();
        public List<Guid> OrderIds { get; set; } = new();
    }

    public static class Seeder
    {
        public static async Task SeedAsync(EducationPlatformDBContext context, SeedOptions options, ILogger logger)
        {
            // Reference data every installation needs
            var gradeIds = await GradeSeeder.SeedAsync(context);
            var subjectIds = await SubjectSeeder.SeedAsync(context);
            await DefaultLessonSeeder.SeedAsync(context, gradeIds, subjectIds);
            await PolicySeeder.SeedAsync(context);

            // The first administrator comes from configuration, never from a password written in the source
            await AdminSeeder.SeedAsync(context, options.Admin, logger);

            if (options.DemoData)
            {
                logger.LogWarning("Seeding demo data (fake users, courses and orders). Do not enable this in production.");

                var userResults = await UserSeeder.SeedAsync(context, options.DemoPassword);
                var courseResults = await CourseSeeder.SeedAsync(context, userResults.TeacherIds, gradeIds, subjectIds);
                await EnrollmentSeeder.SeedAsync(context, userResults.StudentIds, courseResults.CoursePrices);
            }

            await context.SaveChangesAsync();

            await AdminSeeder.WarnAboutLegacyPasswordsAsync(context, logger);
        }
    }
}
