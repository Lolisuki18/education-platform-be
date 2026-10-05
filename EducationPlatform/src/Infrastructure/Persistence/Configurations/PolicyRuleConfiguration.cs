using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Rule (Internal Entity).</summary>
    public class PolicyRuleConfiguration : IEntityTypeConfiguration<PolicyRule>
    {
        public void Configure(EntityTypeBuilder<PolicyRule> entity)
        {
            entity.HasKey(r => r.PolicyRuleID);

            entity.Property(r => r.Code)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(r => r.Description)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(r => r.PolicyID)
                  .IsRequired();
        }
    }
}
