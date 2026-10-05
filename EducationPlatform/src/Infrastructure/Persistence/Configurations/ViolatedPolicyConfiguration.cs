using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Violated Policy (Internal Entity).</summary>
    public class ViolatedPolicyConfiguration : IEntityTypeConfiguration<ViolatedPolicy>
    {
        public void Configure(EntityTypeBuilder<ViolatedPolicy> entity)
        {
            entity.HasKey(l => l.ViolatedPolicyID);

            entity.Property(l => l.PolicyID)
                  .IsRequired();

            entity.Property(l => l.CourseID)
                  .IsRequired();

            entity.HasOne(l => l.Policy)
                  .WithMany()
                  .HasForeignKey(q => q.PolicyID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
