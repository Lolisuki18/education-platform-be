using Domain.CourseManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class PolicySeeder
    {
        public static async Task SeedAsync(EducationPlatformDBContext context)
        {
            if (await context.Policies.AnyAsync())
                return;

            var policies = new List<Policy>();

            // Create policies first
            var contentQualityPolicyId = Guid.NewGuid();
            policies.Add(new Policy(contentQualityPolicyId, "Content Quality Policy"));

            var academicIntegrityPolicyId = Guid.NewGuid();
            policies.Add(new Policy(academicIntegrityPolicyId, "Academic Integrity Policy"));

            var assessmentPolicyId = Guid.NewGuid();
            policies.Add(new Policy(assessmentPolicyId, "Assessment Policy"));

            var contentSafetyPolicyId = Guid.NewGuid();
            policies.Add(new Policy(contentSafetyPolicyId, "Content Safety Policy"));

            var curriculumPolicyId = Guid.NewGuid();
            policies.Add(new Policy(curriculumPolicyId, "Curriculum Alignment Policy"));

            var technicalPolicyId = Guid.NewGuid();
            policies.Add(new Policy(technicalPolicyId, "Technical Quality Policy"));

            var pricingPolicyId = Guid.NewGuid();
            policies.Add(new Policy(pricingPolicyId, "Pricing Policy"));

            context.Policies.AddRange(policies);
            await context.SaveChangesAsync();
        }
    }
}
