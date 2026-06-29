using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.ValueObject;
using Domain.OrderManagement.Aggregate;
using Infrastructure.Persistence;

namespace IntegrationTests
{
    public static class TestSeeder
    {
        public static async Task SeedAsync(EducationPlatformDBContext context)
        {
            // Seed a Grade
            var grade = new Grade(Guid.NewGuid(), "Grade 10");
            context.Set<Grade>().Add(grade);

            // Seed a Subject
            var subject = new Subject(Guid.NewGuid(), "MATH", "Mathematics", grade.GradeID);
            context.Set<Subject>().Add(subject);

            // Seed a Teacher User
            var teacher = new User(
                Guid.NewGuid(),
                "teacher@example.com",
                "Password123!",
                "0911111111",
                "Teacher User",
                "Bio of Teacher",
                Role.Teacher,
                DateTime.Now
            );
            teacher.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            teacher.VerifyEmail(teacher.EmailOtp!);
            context.Set<User>().Add(teacher);

            // Seed a second Teacher User for role authorization checks
            var teacher2 = new User(
                Guid.NewGuid(),
                "teacher2@example.com",
                "Password123!",
                "0944444444",
                "Teacher User 2",
                "Bio of Teacher 2",
                Role.Teacher,
                DateTime.Now
            );
            teacher2.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            teacher2.VerifyEmail(teacher2.EmailOtp!);
            context.Set<User>().Add(teacher2);

            // Seed an Admin User
            var admin = new User(
                Guid.NewGuid(),
                "admin@example.com",
                "Password123!",
                "0933333333",
                "Admin User",
                "Bio of Admin",
                Role.Admin,
                DateTime.Now
            );
            admin.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            admin.VerifyEmail(admin.EmailOtp!);
            context.Set<User>().Add(admin);

            // Seed a Student User
            var student = new User(
                Guid.NewGuid(),
                "student@example.com",
                "Password123!",
                "0922222222",
                "Student User",
                "Bio of Student",
                Role.Student,
                DateTime.Now
            );
            student.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            student.VerifyEmail(student.EmailOtp!);
            context.Set<User>().Add(student);

            // Seed a Coupon for Student
            var coupon = new Coupon(
                Guid.NewGuid(),
                student.UserID,
                "DISCOUNT10",
                10000,
                "Welcome discount"
            );
            context.Set<Coupon>().Add(coupon);

            await context.SaveChangesAsync();

            // Seed 2 Courses (1 Published, 1 Pending/Draft)
            var course1 = new Course(
                Guid.NewGuid(),
                "Math algebra",
                "Algebra course",
                50000,
                "2026/03/default-thumbnail-1.jpg",
                "math-algebra",
                "Basic algebra",
                "Learn equations",
                teacher.UserID,
                grade.GradeID,
                subject.SubjectID,
                DateTime.Now
            );
            course1.MarkAsPublished(DateTime.Now);
            context.Set<Course>().Add(course1);

            var course2 = new Course(
                Guid.NewGuid(),
                "Math geometry",
                "Geometry course",
                60000,
                "2026/03/default-thumbnail-2.jpg",
                "math-geometry",
                "Basic geometry",
                "Learn shapes",
                teacher.UserID,
                grade.GradeID,
                subject.SubjectID,
                DateTime.Now
            );
            // This course remains pending review/draft (not marked published)
            context.Set<Course>().Add(course2);

            // Seed a Chapter for course1
            var chapter = new Chapter(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Introduction to Algebra",
                "Chapter 1: Basics",
                1,
                course1.CourseID
            );
            // Add a Lesson to the Chapter
            var lesson = chapter.AddLesson(
                "Lesson 1: Variables",
                "Understand variables",
                "Learn about variables",
                "https://example.com/video1"
            );
            // Re-assign Guid for testing reproducibility
            var lessonField = typeof(Lesson).GetProperty("LessonID");
            lessonField?.SetValue(lesson, Guid.Parse("22222222-2222-2222-2222-222222222222"));

            context.Set<Chapter>().Add(chapter);
            context.Set<Lesson>().Add(lesson);

            await context.SaveChangesAsync();
        }
    }
}
