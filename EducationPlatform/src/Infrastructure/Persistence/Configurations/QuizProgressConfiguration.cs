using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of QuizProgress (Internal Entity).</summary>
    public class QuizProgressConfiguration : IEntityTypeConfiguration<QuizProgress>
    {
        public void Configure(EntityTypeBuilder<QuizProgress> entity)
        {
            entity.HasKey(qp => qp.QuizProgressID);

            entity.Property(qp => qp.AttemptCount).IsRequired();
            entity.Property(qp => qp.IsCorrect).IsRequired();
            entity.Property(qp => qp.LastAttemptAt);
            entity.Property(qp => qp.QuizID).IsRequired();
            entity.Property(qp => qp.LessonProgressID).IsRequired();

            entity.HasOne(lp => lp.Quiz)
                  .WithMany()
                  .HasForeignKey(lp => lp.QuizID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
