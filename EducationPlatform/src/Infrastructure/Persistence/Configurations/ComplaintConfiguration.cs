using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Complaint (Aggregate Root).</summary>
    public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
    {
        public void Configure(EntityTypeBuilder<Complaint> entity)
        {
            entity.HasKey(c => c.ComplaintID);

            entity.Property(c => c.Reason)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(c => c.EvidenceImagePath)
                  .HasMaxLength(500);

            entity.Property(c => c.Status)
                  .IsRequired();

            entity.Property(c => c.CreatedAt)
                  .IsRequired();

            entity.Property(c => c.ReviewedAt);

            entity.Property(c => c.AdminNote)
                  .HasMaxLength(2000);

            entity.Property(c => c.CourseID)
                  .IsRequired();

            entity.Property(c => c.StudentID)
                  .IsRequired();

            // Relationship with Course
            entity.HasOne(c => c.Course)
                  .WithMany()
                  .HasForeignKey(c => c.CourseID)
                  .OnDelete(DeleteBehavior.Cascade);

            // Relationship with User (Student)
            entity.HasOne(c => c.User)
                  .WithMany()
                  .HasForeignKey(c => c.StudentID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.CourseID);
        }
    }
}
