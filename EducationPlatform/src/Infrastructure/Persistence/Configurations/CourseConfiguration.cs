using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Course (Aggregate Root).</summary>
    public class CourseConfiguration : IEntityTypeConfiguration<Course>
    {
        public void Configure(EntityTypeBuilder<Course> entity)
        {
            entity.HasKey(c => c.CourseID);

            entity.Property(c => c.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(c => c.Description)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(c => c.Status)
                  .IsRequired();

            // ----- CoursePrice (Value Object)
            entity.OwnsOne(c => c.Price, price =>
            {
                price.Property(p => p.Amount)
                     .HasColumnName("PriceAmount")
                     .IsRequired();
            });

            entity.Property(c => c.ThumbnailName)
                  .HasMaxLength(255);

            entity.Property(c => c.Slug)
                  .IsRequired()
                  .HasMaxLength(Domain.Common.Slugs.MaxLength);

            entity.HasIndex(c => c.Slug)
                  .IsUnique();

            entity.Property(c => c.Prerequisites)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(c => c.LearningOutcomes)
                  .IsRequired()
                  .HasMaxLength(4000);

            entity.Property(c => c.RejectedAt);
            entity.Property(c => c.AdminNote);
            entity.Property(c => c.PublishedAt);
            entity.Property(c => c.CreatedAt);

            entity.Property(c => c.TeacherID).IsRequired();
            entity.Property(c => c.GradeID).IsRequired();
            entity.Property(c => c.SubjectID).IsRequired();

            // ----- Populate (Other Domain)
            entity.HasOne(c => c.Teacher)
                  .WithMany()
                  .HasForeignKey(c => c.TeacherID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Grade)
                  .WithMany()
                  .HasForeignKey(c => c.GradeID)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.Subject)
                  .WithMany()
                  .HasForeignKey(c => c.SubjectID)
                  .OnDelete(DeleteBehavior.Restrict);

            // ----- Violated Policy (Internal Entities)
            entity.HasMany(c => c.ViolatedPolicies)
                  .WithOne()
                  .HasForeignKey(l => l.CourseID)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(c => c.Status);
            entity.HasIndex(c => c.TeacherID);

            // ----- Chapter (Internal Entities)
            entity.HasMany(c => c.Chapters)
                  .WithOne()
                  .HasForeignKey(l => l.CourseID)
                  .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
