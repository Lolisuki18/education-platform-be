using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Payment (Aggregate Root).</summary>
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> entity)
        {
            entity.HasKey(p => p.OrderID);

            entity.Property(p => p.OrderCode)
                  .IsRequired();

            entity.Property(p => p.StudentID)
                  .IsRequired();

            entity.Property(p => p.CourseID)
                  .IsRequired();

            entity.Property(p => p.PlatformAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(p => p.TeacherAmount)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(p => p.Method)
                  .IsRequired();

            entity.Property(p => p.Status)
                  .IsRequired()
                  .IsConcurrencyToken();

            entity.Property(p => p.CreatedAt)
                  .IsRequired();

            entity.Property(p => p.PaidAt);

            entity.Property(p => p.CheckoutUrl)
                  .HasMaxLength(2000);

            entity.Property(p => p.CouponIds)
                  .HasColumnType("uuid[]");

            // Relationship with Teacher (User)
            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.StudentID)
                  .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Course
            entity.HasOne(p => p.Course)
                  .WithMany()
                  .HasForeignKey(p => p.CourseID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.StudentID);

            // OrderStatus.Created = 1: a student can only have one order awaiting payment per course
            entity.HasIndex(p => new { p.StudentID, p.CourseID })
                  .HasFilter("\"Status\" = 1")
                  .IsUnique();

            entity.HasIndex(p => p.OrderCode);
        }
    }
}
