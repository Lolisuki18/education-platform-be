using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of DefaultLesson (Internal Entity).</summary>
    public class DefaultLessonConfiguration : IEntityTypeConfiguration<DefaultLesson>
    {
        public void Configure(EntityTypeBuilder<DefaultLesson> entity)
        {
            entity.HasKey(d => d.DefaultLessonID);

            entity.Property(d => d.Name)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(d => d.Description)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(d => d.Objectives)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(d => d.IsActive)
                  .HasDefaultValue(true);

            entity.Property(d => d.SubjectID)
                  .IsRequired();

            entity.Property(d => d.GradeID)
                  .IsRequired();

            entity.HasIndex(d => new { d.SubjectID, d.GradeID });

            // The relationship with Subject is configured once, from the Subject side (SubjectConfiguration)

            entity.HasOne<Grade>()
                  .WithMany()
                  .HasForeignKey(d => d.GradeID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
