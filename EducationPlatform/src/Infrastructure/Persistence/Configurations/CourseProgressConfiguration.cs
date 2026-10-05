using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of CourseProgress (Internal Entity).</summary>
    public class CourseProgressConfiguration : IEntityTypeConfiguration<CourseProgress>
    {
        public void Configure(EntityTypeBuilder<CourseProgress> entity)
        {
            entity.HasKey(cp => cp.CourseProgressID);

            entity.Property(cp => cp.CompletionRate)
                  .HasPrecision(5, 2);

            entity.Property(cp => cp.IsCompleted).IsRequired();
            entity.Property(cp => cp.EnrollmentID).IsRequired();

            // Optimistic concurrency: prevents two concurrent lesson/quiz submissions
            // for the same enrollment from silently overwriting each other's recalculated progress.
            entity.Property<uint>("xmin")
                  .HasColumnName("xmin")
                  .HasColumnType("xid")
                  .ValueGeneratedOnAddOrUpdate()
                  .IsConcurrencyToken();

            entity.HasMany(cp => cp.ChapterProgresses)
                  .WithOne()
                  .HasForeignKey(lp => lp.CourseProgressID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
