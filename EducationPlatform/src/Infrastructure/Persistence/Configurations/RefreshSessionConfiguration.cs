using Domain.IdentityManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of RefreshSession.</summary>
    public class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
    {
        public void Configure(EntityTypeBuilder<RefreshSession> entity)
        {
            entity.HasKey(s => s.SessionID);

            // Keys are assigned by the domain; without this EF would treat a session added to a tracked user as existing.
            entity.Property(s => s.SessionID)
                  .ValueGeneratedNever();

            entity.Property(s => s.Hash)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(s => s.CreatedAt).IsRequired();
            entity.Property(s => s.ExpiresAt).IsRequired();

            // Two concurrent refreshes with the same token must not both succeed.
            entity.Property(s => s.RevokedAt)
                  .IsConcurrencyToken();

            entity.HasIndex(s => s.Hash)
                  .IsUnique();

            entity.HasIndex(s => s.UserID);
        }
    }
}
