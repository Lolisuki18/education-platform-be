using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Material (Internal Entity).</summary>
    public class MaterialConfiguration : IEntityTypeConfiguration<Material>
    {
        public void Configure(EntityTypeBuilder<Material> entity)
        {
            entity.HasKey(m => m.MaterialID);

            entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Description).HasMaxLength(2000);
            entity.Property(m => m.Url).IsRequired().HasMaxLength(1000);
            entity.Property(m => m.Type).IsRequired();
            entity.Property(m => m.LessonID).IsRequired();
        }
    }
}
