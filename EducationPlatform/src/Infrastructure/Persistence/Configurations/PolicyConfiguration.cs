using Domain.CourseManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Policy (Aggregate Root).</summary>
    public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
    {
        public void Configure(EntityTypeBuilder<Policy> entity)
        {
            entity.HasKey(p => p.PolicyID);

            entity.Property(p => p.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(p => p.IsActive)
                  .HasDefaultValue(true);

            entity.HasMany(p => p.PolicyRules)
                  .WithOne()
                  .HasForeignKey(r => r.PolicyID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
