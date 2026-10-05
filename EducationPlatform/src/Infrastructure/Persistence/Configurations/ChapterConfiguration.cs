using Domain.CourseManagement.Entity;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Chapter (Internal Entity).</summary>
    public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
    {
        public void Configure(EntityTypeBuilder<Chapter> entity)
        {
            entity.HasKey(c => c.ChapterID);

            entity.Property(c => c.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(c => c.Description)
                  .HasMaxLength(2000);

            entity.Property(c => c.Order)
                  .IsRequired();

            entity.Property(c => c.IsViolated);
            entity.Property(c => c.AdminNote);

            entity.Property(c => c.CourseID).IsRequired();

            // ----- Lesson (Internal Entities)
            entity.HasMany(c => c.Lessons)
                  .WithOne()
                  .HasForeignKey(l => l.ChapterID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
