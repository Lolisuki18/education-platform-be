using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of CourseReview (Entity).</summary>
    public class CourseReviewConfiguration : IEntityTypeConfiguration<CourseReview>
    {
        public void Configure(EntityTypeBuilder<CourseReview> entity)
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Rating)
                  .IsRequired();

            entity.Property(r => r.Comment)
                  .HasMaxLength(2000);

            entity.Property(r => r.CreatedAt)
                  .IsRequired();

            entity.Property(r => r.CourseID)
                  .IsRequired();

            entity.Property(r => r.StudentID)
                  .IsRequired();

            // Relationships
            entity.HasOne(r => r.Course)
                  .WithMany()
                  .HasForeignKey(r => r.CourseID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Student)
                  .WithMany()
                  .HasForeignKey(r => r.StudentID)
                  .OnDelete(DeleteBehavior.Restrict);

            // A student reviews a course once; the handler checks it, this makes it hold under concurrency
            entity.HasIndex(r => new { r.StudentID, r.CourseID })
                  .IsUnique();
        }
    }
}
