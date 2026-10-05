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
using Domain.NotificationManagement.Aggregate;
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
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // One IEntityTypeConfiguration per entity, in the Configurations folder
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(EducationPlatformDBContext).Assembly);
        }
    }
}
