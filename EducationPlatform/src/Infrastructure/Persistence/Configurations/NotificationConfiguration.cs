using Domain.IdentityManagement.Aggregate;
using Domain.NotificationManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Notification (Aggregate Root).</summary>
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> entity)
        {
            entity.HasKey(n => n.NotificationID);

            entity.Property(n => n.NotificationID).ValueGeneratedNever();

            entity.Property(n => n.Title)
                  .IsRequired()
                  .HasMaxLength(Notification.MaxTitleLength);

            entity.Property(n => n.Message)
                  .IsRequired()
                  .HasMaxLength(Notification.MaxMessageLength);

            entity.Property(n => n.CreatedAt).IsRequired();
            entity.Property(n => n.ReadAt);

            entity.Ignore(n => n.IsRead);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(n => n.UserID)
                  .OnDelete(DeleteBehavior.Cascade);

            // "my notifications, newest first" and the unread badge
            entity.HasIndex(n => new { n.UserID, n.CreatedAt });
            entity.HasIndex(n => new { n.UserID, n.ReadAt });
        }
    }
}
