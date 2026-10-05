using Domain.AuditManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of AuditLog (Aggregate Root).</summary>
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> entity)
        {
            entity.HasKey(a => a.AuditLogId);

            entity.Property(a => a.EntityName).IsRequired();
            entity.Property(a => a.Action).IsRequired();
            entity.Property(a => a.PerformedBy).HasMaxLength(100);
            entity.Property(a => a.OldValue);
            entity.Property(a => a.NewValue);

            entity.Property(a => a.Timestamp)
                  .HasDefaultValueSql("now()");
        }
    }
}
