using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Subject (Aggregate Root).</summary>
    public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
    {
        public void Configure(EntityTypeBuilder<Subject> entity)
        {
            entity.HasKey(s => s.SubjectID);

            entity.Property(s => s.Code)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(s => s.Name)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(s => s.IsActive)
                  .HasDefaultValue(true);

            entity.HasIndex(s => s.Code)
                  .IsUnique();

            // ----- Default Lessons (Internal Entities)
            entity.HasMany(s => s.DefaultLessons)
                  .WithOne()
                  .HasForeignKey(nameof(DefaultLesson.SubjectID))
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
