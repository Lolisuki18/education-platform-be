using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.ValueObject;
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

            await context.SaveChangesAsync();
        }
    }
}
