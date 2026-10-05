using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Assignment (Internal Entity).</summary>
    public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
    {
        public void Configure(EntityTypeBuilder<Assignment> entity)
        {
            entity.HasKey(a => a.AssignmentID);

            entity.Property(a => a.Title).IsRequired().HasMaxLength(200);
            entity.Property(a => a.Description).HasMaxLength(2000);
            entity.Property(a => a.MaxScore).IsRequired();
            entity.Property(a => a.LessonID).IsRequired();
        }
    }
}
