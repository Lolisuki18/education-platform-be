using Domain.AcademicManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Grade (Aggregate Root).</summary>
    public class GradeConfiguration : IEntityTypeConfiguration<Grade>
    {
        public void Configure(EntityTypeBuilder<Grade> entity)
        {
            entity.HasKey(g => g.GradeID);

            entity.Property(g => g.Name)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(g => g.IsActive)
                  .HasDefaultValue(true);

            entity.HasIndex(g => g.Name)
                  .IsUnique();
        }
    }
}
