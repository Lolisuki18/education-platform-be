using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of LessonProgress (Internal Entity).</summary>
    public class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
    {
        public void Configure(EntityTypeBuilder<LessonProgress> entity)
        {
            entity.HasKey(lp => lp.LessonProgressID);

            entity.Property(lp => lp.IsCompleted).IsRequired();
            entity.Property(lp => lp.CompletedAt);
            entity.Property(lp => lp.ChapterProgressID).IsRequired();
            entity.Property(lp => lp.LessonID).IsRequired();

            entity.HasMany(lp => lp.QuizProgresses)
                  .WithOne()
                  .HasForeignKey(qp => qp.LessonProgressID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lp => lp.Lesson)
                  .WithMany()
                  .HasForeignKey(lp => lp.LessonID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
