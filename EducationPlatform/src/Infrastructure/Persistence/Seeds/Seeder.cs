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
        public static async Task SeedAsync(EducationPlatformDBContext context)
        {
            // Academic management
            var gradeIds = await GradeSeeder.SeedAsync(context);
            var subjectIds = await SubjectSeeder.SeedAsync(context);
            await DefaultLessonSeeder.SeedAsync(context, gradeIds, subjectIds);

            // Identity management
            var userResults = await UserSeeder.SeedAsync(context);

            // Course management
            var courseResults = await CourseSeeder.SeedAsync(context, userResults.TeacherIds, gradeIds, subjectIds);
            await PolicySeeder.SeedAsync(context);

            // Enrollment management / Payment management
            var enrollmentResult = await EnrollmentSeeder.SeedAsync(context, userResults.StudentIds, courseResults.CoursePrices);

            await context.SaveChangesAsync();
        }
    }
}

