using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of ChapterProgress (Internal Entity).</summary>
    public class ChapterProgressConfiguration : IEntityTypeConfiguration<ChapterProgress>
    {
        public void Configure(EntityTypeBuilder<ChapterProgress> entity)
        {
            entity.HasKey(cp => cp.ChapterProgressID);

            entity.Property(cp => cp.IsCompleted)
                  .IsRequired();

            entity.Property(cp => cp.CourseProgressID)
                  .IsRequired();

            entity.Property(cp => cp.ChapterID)
                  .IsRequired();

            // ----- Relation to LessonProgress
            entity.HasMany(cp => cp.LessonProgresses)
                  .WithOne()
                  .HasForeignKey(lp => lp.ChapterProgressID) // FK in LessonProgress
                  .OnDelete(DeleteBehavior.Cascade);

            // ----- Relation to Chapter (optional navigation if needed)
            entity.HasOne(cp => cp.Chapter)
                  .WithMany()
                  .HasForeignKey(cp => cp.ChapterID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
