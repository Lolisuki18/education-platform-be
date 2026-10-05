using Domain.CourseManagement.Entity;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Lesson (Internal Entity).</summary>
    public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
    {
        public void Configure(EntityTypeBuilder<Lesson> entity)
        {
            entity.HasKey(l => l.LessonID);

            entity.Property(l => l.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(l => l.Objectives)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(l => l.Description)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(l => l.VideoUrl)
                  .IsRequired()
                  .HasMaxLength(1000);

            entity.Property(l => l.Order)
                  .IsRequired();

            entity.Property(l => l.ChapterID)
                .IsRequired();

            entity.HasMany(l => l.Quizzes)
                  .WithOne()
                  .HasForeignKey(q => q.LessonID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Assignments)
                  .WithOne()
                  .HasForeignKey(q => q.LessonID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(l => l.Materials)
                  .WithOne()
                  .HasForeignKey(q => q.LessonID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
