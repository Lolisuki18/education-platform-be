using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Enrollment (Aggregate Root).</summary>
    public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
    {
        public void Configure(EntityTypeBuilder<Enrollment> entity)
        {
            entity.HasKey(e => e.EnrollmentID);

            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.EnrolledAt).IsRequired();
            entity.Property(e => e.CompletedAt);

            entity.Property(e => e.StudentID).IsRequired();
            entity.Property(e => e.CourseID).IsRequired();

            // 1 - 1 with CourseProgress
            entity.HasOne(e => e.CourseProgress)
                  .WithOne()
                  .HasForeignKey<CourseProgress>(cp => cp.EnrollmentID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lp => lp.Course)
                  .WithMany()
                  .HasForeignKey(lp => lp.CourseID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.StudentID);
            entity.HasIndex(e => e.Status);

            // One enrollment per student and course, however many requests race to create it
            entity.HasIndex(e => new { e.StudentID, e.CourseID })
                  .IsUnique();
        }
    }
}
