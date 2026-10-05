using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Coupon (Aggregate Root).</summary>
    public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
    {
        public void Configure(EntityTypeBuilder<Coupon> entity)
        {
            entity.HasKey(c => c.CouponID);

            entity.Property(c => c.Code)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.HasIndex(c => c.Code)
                  .IsUnique();

            entity.HasIndex(c => c.StudentID);

            entity.Property(c => c.DiscountAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(c => c.IsUsed)
                  .IsRequired();

            entity.Property(c => c.StudentID)
                  .IsRequired(false);

            entity.Property(c => c.Reason)
                  .HasMaxLength(500);

            entity.Property(c => c.Description)
                  .HasMaxLength(1000);

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            entity.Property(c => c.UpdatedAt)
                  .IsRequired(false);

            entity.Property(c => c.StartDate)
                  .IsRequired();

            entity.Property(c => c.ExpiredDate)
                  .IsRequired();

            entity.Property(c => c.MaxUsage)
                  .IsRequired();

            entity.Property(c => c.CurrentUsage)
                  .IsRequired();

            entity.Property(c => c.IsActive)
                  .IsRequired();

            entity.Property(c => c.Type)
                  .IsRequired();

            entity.Property(c => c.Version)
                  .IsRequired()
                  .IsConcurrencyToken();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(c => c.StudentID)
                  .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
