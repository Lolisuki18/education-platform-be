using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations
{
    /// <summary>Table mapping of Quiz (Internal Entity).</summary>
    public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
    {
        public void Configure(EntityTypeBuilder<Quiz> entity)
        {
            entity.HasKey(q => q.QuizID);

            entity.Property(q => q.Question)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(q => q.Note)
                  .HasMaxLength(2000);

            entity.Property(q => q.LessonID)
                  .IsRequired();

            // ----- QuizAnswer (Value Object)
            entity.OwnsOne(q => q.Answer, answer =>
            {
                answer.Property(a => a.Type)
                      .HasColumnName("AnswerType")
                      .IsRequired();

                answer.Property(a => a.CorrectAnswers)
                      .HasColumnName("CorrectAnswers")
                      .HasConversion(
                          v => string.Join("|||", v), // <-- changed delimiter
                          v => v.Split(new[] { "|||" }, StringSplitOptions.None)
                                .Select(s => s.Trim())
                                .ToList())
                      .IsRequired();

                answer.Property(a => a.Options)
                      .HasColumnName("Options")
                      .HasConversion(
                          v => v != null ? string.Join("|||", v) : null,
                          v => !string.IsNullOrWhiteSpace(v)
                               ? v.Split(new[] { "|||" }, StringSplitOptions.None).ToList()
                               : new List<string>())
                      .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IEnumerable<string>>(
                          (c1, c2) => ReferenceEquals(c1, c2) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                          c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                          c => c.ToList()));
            });
        }
    }
}
