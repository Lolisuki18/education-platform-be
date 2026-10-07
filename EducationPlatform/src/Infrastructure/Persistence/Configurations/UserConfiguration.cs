using Domain.IdentityManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of User (Aggregate Root).</summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> entity)
        {
            entity.HasKey(u => u.UserID);

            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(u => u.Phone)
                  .IsRequired()
                  .HasMaxLength(20);

            entity.Property(u => u.Name)
                  .IsRequired()
                  .HasMaxLength(150);

            entity.Property(u => u.Bio)
                  .HasMaxLength(1000);

            entity.Property(u => u.Role)
                  .IsRequired()
                  .HasConversion<int>();

            entity.Property(u => u.IsVerified)
                  .HasDefaultValue(false);

            entity.Property(u => u.EmailOtp);

            entity.Property(u => u.EmailOtpExpiresAt);

            entity.Property(u => u.PasswordResetOtp);

            entity.Property(u => u.PasswordResetOtpExpiresAt);

            entity.Property(u => u.CreatedAt);

            entity.Property(u => u.DeletedAt);

            entity.Property(u => u.TokensValidFrom);

            entity.Property(u => u.IsActive)
                  .HasDefaultValue(true);

            // ---------- Password (Value Object)
            entity.OwnsOne(u => u.Password, pw =>
            {
                pw.Property(p => p.Hash)
                  .HasColumnName("PasswordHash")
                  .IsRequired()
                  .HasMaxLength(500);
            });

            // ---------- Refresh sessions (one per device)
            entity.HasMany(u => u.RefreshSessions)
                  .WithOne()
                  .HasForeignKey(s => s.UserID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(u => u.RefreshSessions)
                  .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(u => u.Email)
                  .IsUnique();

            entity.HasIndex(u => u.Phone)
                  .IsUnique();

            entity.HasIndex(u => u.EmailOtp);
        }
    }
}
