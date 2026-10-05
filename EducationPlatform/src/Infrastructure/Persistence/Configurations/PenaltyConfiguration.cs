using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Penalty (Aggregate Root).</summary>
    public class PenaltyConfiguration : IEntityTypeConfiguration<Penalty>
    {
        public void Configure(EntityTypeBuilder<Penalty> entity)
        {
            entity.HasKey(p => p.PenaltyID);

            entity.Property(p => p.PenaltyAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(p => p.Reason)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(p => p.CreatedAt)
                  .IsRequired();

            entity.Property(p => p.TeacherID)
                  .IsRequired();

            entity.Property(p => p.CourseID)
                  .IsRequired();

            // Relationship with Teacher (User)
            entity.HasOne(p => p.Teacher)
                  .WithMany()
                  .HasForeignKey(p => p.TeacherID)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Course
            entity.HasOne(p => p.Course)
                  .WithMany()
                  .HasForeignKey(p => p.CourseID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
