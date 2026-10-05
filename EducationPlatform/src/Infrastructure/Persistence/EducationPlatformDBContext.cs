using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Domain.AuditManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Entity;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence
{
    public class EducationPlatformDBContext : DbContext, IApplicationDBContext
    {
        public EducationPlatformDBContext(
            DbContextOptions<EducationPlatformDBContext> options)
            : base(options) { }

        // ====================
        // Academic Management
        // ====================
        public DbSet<Grade> Grades => Set<Grade>();
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<DefaultLesson> DefaultLessons => Set<DefaultLesson>();

        // ====================
        // Identity Management
        // ====================
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

        // ====================
        // Course Management
        // ====================
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Lesson> Lessons => Set<Lesson>();
        public DbSet<Quiz> Quizzes => Set<Quiz>();
        public DbSet<Assignment> Assignments => Set<Assignment>();
        public DbSet<Material> Materials => Set<Material>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<ViolatedPolicy> ViolatedPolicies => Set<ViolatedPolicy>();
        public DbSet<PolicyRule> PolicyRules => Set<PolicyRule>();
        public DbSet<CourseReview> CourseReviews => Set<CourseReview>();

        // ====================
        // Payment Management
        // ====================
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Penalty> Penalties => Set<Penalty>();

        // ====================
        // Enrollment Management
        // ====================
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<CourseProgress> CourseProgresses => Set<CourseProgress>();
        public DbSet<ChapterProgress> ChapterProgresses => Set<ChapterProgress>();
        public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
        public DbSet<QuizProgress> QuizProgresses => Set<QuizProgress>();


        // ====================
        // Audit Management
        // ====================
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ====================
            // Grade (Aggregate Root)
            // ====================
            modelBuilder.Entity<Grade>(entity =>
            {
                entity.HasKey(g => g.GradeID);

                entity.Property(g => g.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(g => g.IsActive)
                      .HasDefaultValue(true);

                entity.HasIndex(g => g.Name)
                      .IsUnique();
            });

            // ====================
            // Subject (Aggregate Root)
            // ====================
            modelBuilder.Entity<Subject>(entity =>
            {
                entity.HasKey(s => s.SubjectID);

                entity.Property(s => s.Code)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(s => s.Name)
                      .IsRequired()
                      .HasMaxLength(150);

                entity.Property(s => s.IsActive)
                      .HasDefaultValue(true);

                entity.HasIndex(s => s.Code)
                      .IsUnique();

                // ----- Default Lessons (Internal Entities)
                entity.HasMany(s => s.DefaultLessons)
                      .WithOne()
                      .HasForeignKey(nameof(DefaultLesson.SubjectID))
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // DefaultLesson (Internal Entity)
            // ====================
            modelBuilder.Entity<DefaultLesson>(entity =>
            {
                entity.HasKey(d => d.DefaultLessonID);

                entity.Property(d => d.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(d => d.Description)
                      .IsRequired()
                      .HasMaxLength(4000);

                entity.Property(d => d.Objectives)
                      .IsRequired()
                      .HasMaxLength(2000);

                entity.Property(d => d.IsActive)
                      .HasDefaultValue(true);

                entity.Property(d => d.SubjectID)
                      .IsRequired();

                entity.Property(d => d.GradeID)
                      .IsRequired();

                entity.HasIndex(d => new { d.SubjectID, d.GradeID });

                entity.HasOne<Subject>()
                      .WithMany()
                      .HasForeignKey(d => d.SubjectID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Grade>()
                      .WithMany()
                      .HasForeignKey(d => d.GradeID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // User (Aggregate Root)
            // ====================
            modelBuilder.Entity<User>(entity =>
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

                entity.Property(u => u.CreatedAt);

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
            });

            // ====================
            // RefreshSession
            // ====================
            modelBuilder.Entity<RefreshSession>(entity =>
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
            });

            // ====================
            // Course (Aggregate Root)
            // ====================
            modelBuilder.Entity<Course>(entity =>
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
                      .HasMaxLength(4000);

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
            });

            // ====================
            // Complaint (Aggregate Root)
            // ====================
            modelBuilder.Entity<Complaint>(entity =>
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
            });

            // ====================
            // Chapter (Internal Entity)
            // ====================
            modelBuilder.Entity<Chapter>(entity =>
            {
                entity.HasKey(c => c.ChapterID);

                entity.Property(c => c.Title)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(c => c.Description)
                      .HasMaxLength(2000);

                entity.Property(c => c.Order)
                      .IsRequired();

                entity.Property(c => c.IsViolated);
                entity.Property(c => c.AdminNote);

                entity.Property(c => c.CourseID).IsRequired();

                // ----- Lesson (Internal Entities)
                entity.HasMany(c => c.Lessons)
                      .WithOne()
                      .HasForeignKey(l => l.ChapterID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================
            // Lesson (Internal Entity)
            // ====================
            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.HasKey(l => l.LessonID);

                entity.Property(l => l.Title)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(l => l.Objectives)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(l => l.Description)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(l => l.VideoUrl)
                      .IsRequired()
                      .HasMaxLength(1000);

                entity.Property(l => l.Order)
                      .IsRequired();

                entity.Property(l => l.ChapterID)
                    .IsRequired();

                entity.HasMany(l => l.Quizzes)
                      .WithOne()
                      .HasForeignKey(q => q.LessonID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(l => l.Assignments)
                      .WithOne()
                      .HasForeignKey(q => q.LessonID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(l => l.Materials)
                      .WithOne()
                      .HasForeignKey(q => q.LessonID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================
            // Quiz (Internal Entity)
            // ====================
            modelBuilder.Entity<Quiz>(entity =>
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
                              (c1, c2) => c1.SequenceEqual(c2),
                              c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                              c => c.ToList()));
                });
            });

            // ====================
            // Assignment (Internal Entity)
            // ====================
            modelBuilder.Entity<Assignment>(entity =>
            {
                entity.HasKey(a => a.AssignmentID);

                entity.Property(a => a.Title).IsRequired().HasMaxLength(200);
                entity.Property(a => a.Description).HasMaxLength(2000);
                entity.Property(a => a.MaxScore).IsRequired();
                entity.Property(a => a.LessonID).IsRequired();
            });

            // ====================
            // Material (Internal Entity)
            // ====================
            modelBuilder.Entity<Material>(entity =>
            {
                entity.HasKey(m => m.MaterialID);

                entity.Property(m => m.Name).IsRequired().HasMaxLength(200);
                entity.Property(m => m.Description).HasMaxLength(2000);
                entity.Property(m => m.Url).IsRequired().HasMaxLength(1000);
                entity.Property(m => m.Type).IsRequired();
                entity.Property(m => m.LessonID).IsRequired();
            });

            // ====================
            // Policy (Aggregate Root)
            // ====================
            modelBuilder.Entity<Policy>(entity =>
            {
                entity.HasKey(p => p.PolicyID);

                entity.Property(p => p.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                entity.Property(p => p.IsActive)
                      .HasDefaultValue(true);

                entity.HasMany(p => p.PolicyRules)
                      .WithOne()
                      .HasForeignKey(r => r.PolicyID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================
            // Violated Policy (Internal Entity)
            // ====================
            modelBuilder.Entity<ViolatedPolicy>(entity =>
            {
                entity.HasKey(l => l.ViolatedPolicyID);

                entity.Property(l => l.PolicyID)
                      .IsRequired();

                entity.Property(l => l.CourseID)
                      .IsRequired();

                entity.HasOne(l => l.Policy)
                      .WithMany()
                      .HasForeignKey(q => q.PolicyID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================
            // Rule (Internal Entity)
            // ====================
            modelBuilder.Entity<PolicyRule>(entity =>
            {
                entity.HasKey(r => r.PolicyRuleID);

                entity.Property(r => r.Code)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(r => r.Description)
                      .IsRequired()
                      .HasMaxLength(2000);

                entity.Property(r => r.PolicyID)
                      .IsRequired();
            });

            // ====================
            // Coupon (Aggregate Root)
            // ====================
            modelBuilder.Entity<Coupon>(entity =>
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
            });

            // ====================
            // Payment (Aggregate Root)
            // ====================
            modelBuilder.Entity<Order>(entity =>
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
            });

            // ====================
            // Penalty (Aggregate Root)
            // ====================
            modelBuilder.Entity<Penalty>(entity =>
            {
                entity.HasKey(p => p.PenaltyID);

                entity.Property(p => p.PenaltyAmount)
                      .IsRequired()
                      .HasPrecision(18, 2);

                entity.Property(p => p.Reason)
                      .IsRequired()
                      .HasMaxLength(500);

                entity.Property(p => p.CreatedAt)
                      .IsRequired();

                entity.Property(p => p.TeacherID)
                      .IsRequired();

                entity.Property(p => p.CourseID)
                      .IsRequired();

                // Relationship with Teacher (User)
                entity.HasOne(p => p.Teacher)
                      .WithMany()
                      .HasForeignKey(p => p.TeacherID)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relationship with Course
                entity.HasOne(p => p.Course)
                      .WithMany()
                      .HasForeignKey(p => p.CourseID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // Enrollment (Aggregate Root)
            // ====================
            modelBuilder.Entity<Enrollment>(entity =>
            {
                entity.HasKey(e => e.EnrollmentID);

                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.EnrolledAt).IsRequired();
                entity.Property(e => e.CompletedAt);

                entity.Property(e => e.StudentID).IsRequired();
                entity.Property(e => e.CourseID).IsRequired();

                // 1 - 1 with CourseProgress
                entity.HasOne(e => e.CourseProgress)
                      .WithOne()
                      .HasForeignKey<CourseProgress>(cp => cp.EnrollmentID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(lp => lp.Course)
                      .WithMany()
                      .HasForeignKey(lp => lp.CourseID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.StudentID);
                entity.HasIndex(e => e.Status);
            });

            // ====================
            // CourseProgress (Internal Entity)
            // ====================
            modelBuilder.Entity<CourseProgress>(entity =>
            {
                entity.HasKey(cp => cp.CourseProgressID);

                entity.Property(cp => cp.CompletionRate)
                      .HasPrecision(5, 2);

                entity.Property(cp => cp.IsCompleted).IsRequired();
                entity.Property(cp => cp.EnrollmentID).IsRequired();

                // Optimistic concurrency: prevents two concurrent lesson/quiz submissions
                // for the same enrollment from silently overwriting each other's recalculated progress.
                entity.Property<uint>("xmin")
                      .HasColumnName("xmin")
                      .HasColumnType("xid")
                      .ValueGeneratedOnAddOrUpdate()
                      .IsConcurrencyToken();

                entity.HasMany(cp => cp.ChapterProgresses)
                      .WithOne()
                      .HasForeignKey(lp => lp.CourseProgressID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ====================
            // ChapterProgress (Internal Entity)
            // ====================
            modelBuilder.Entity<ChapterProgress>(entity =>
            {
                entity.HasKey(cp => cp.ChapterProgressID);

                entity.Property(cp => cp.IsCompleted)
                      .IsRequired();

                entity.Property(cp => cp.CourseProgressID)
                      .IsRequired();

                entity.Property(cp => cp.ChapterID)
                      .IsRequired();

                // ----- Relation to LessonProgress
                entity.HasMany(cp => cp.LessonProgresses)
                      .WithOne()
                      .HasForeignKey(lp => lp.ChapterProgressID) // FK in LessonProgress
                      .OnDelete(DeleteBehavior.Cascade);

                // ----- Relation to Chapter (optional navigation if needed)
                entity.HasOne(cp => cp.Chapter)
                      .WithMany()
                      .HasForeignKey(cp => cp.ChapterID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // LessonProgress (Internal Entity)
            // ====================
            modelBuilder.Entity<LessonProgress>(entity =>
            {
                entity.HasKey(lp => lp.LessonProgressID);

                entity.Property(lp => lp.IsCompleted).IsRequired();
                entity.Property(lp => lp.CompletedAt);
                entity.Property(lp => lp.ChapterProgressID).IsRequired();
                entity.Property(lp => lp.LessonID).IsRequired();

                entity.HasMany(lp => lp.QuizProgresses)
                      .WithOne()
                      .HasForeignKey(qp => qp.LessonProgressID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(lp => lp.Lesson)
                      .WithMany()
                      .HasForeignKey(lp => lp.LessonID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // QuizProgress (Internal Entity)
            // ====================
            modelBuilder.Entity<QuizProgress>(entity =>
            {
                entity.HasKey(qp => qp.QuizProgressID);

                entity.Property(qp => qp.AttemptCount).IsRequired();
                entity.Property(qp => qp.IsCorrect).IsRequired();
                entity.Property(qp => qp.LastAttemptAt);
                entity.Property(qp => qp.QuizID).IsRequired();
                entity.Property(qp => qp.LessonProgressID).IsRequired();

                entity.HasOne(lp => lp.Quiz)
                      .WithMany()
                      .HasForeignKey(lp => lp.QuizID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ====================
            // CourseReview (Entity)
            // ====================
            modelBuilder.Entity<CourseReview>(entity =>
            {
                entity.HasKey(r => r.Id);

                entity.Property(r => r.Rating)
                      .IsRequired();

                entity.Property(r => r.Comment)
                      .HasMaxLength(2000);

                entity.Property(r => r.CreatedAt)
                      .IsRequired();

                entity.Property(r => r.CourseID)
                      .IsRequired();

                entity.Property(r => r.StudentID)
                      .IsRequired();

                // Relationships
                entity.HasOne(r => r.Course)
                      .WithMany()
                      .HasForeignKey(r => r.CourseID)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Student)
                      .WithMany()
                      .HasForeignKey(r => r.StudentID)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            // ====================
            // AuditLog (Aggregate Root)
            // ====================
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.AuditLogId);

                entity.Property(a => a.EntityName).IsRequired();
                entity.Property(a => a.Action).IsRequired();
                entity.Property(a => a.PerformedBy).HasMaxLength(100);
                entity.Property(a => a.OldValue);
                entity.Property(a => a.NewValue);

                entity.Property(a => a.Timestamp)
                      .HasDefaultValueSql("now()");
            });
        }
    }
}
