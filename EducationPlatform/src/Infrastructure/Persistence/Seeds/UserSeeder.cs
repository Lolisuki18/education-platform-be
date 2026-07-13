using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class UserSeeder
    {
        public static async Task<UserSeedResult> SeedAsync(EducationPlatformDBContext context)
        {
            var result = new UserSeedResult();

            // ====================
            // Admin
            // ====================
            if (!await context.Users.AnyAsync(u => u.Role == Role.Admin))
            {
                var admin = new User(
                    Guid.NewGuid(),
                    "longdong32120@gmail.com",
                    "28012005",
                    "0000000000",
                    "Dong Xuan Bao Long",
                    "Platform Administrator",
                    Role.Admin,
                    null,
                    true
                );

                context.Users.Add(admin);
                result.AdminIds.Add(admin.UserID);
            }
            else
            {
                result.AdminIds = await context.Users
                    .Where(u => u.Role == Role.Admin)
                    .Select(u => u.UserID)
                    .ToListAsync();
            }

            // ====================
            // Nai Teacher
            // ====================
            if (!await context.Users.AnyAsync(u => u.Email == "nnnai3131@gmail.com"))
            {
                var naiTeacher = new User(
                    Guid.NewGuid(),
                    "nnnai3131@gmail.com",
                    "28012005",
                    "0000000001",
                    "Nai Teacher",
                    "Teacher account",
                    Role.Teacher,
                    null,
                    true
                );

                context.Users.Add(naiTeacher);
                result.TeacherIds.Add(naiTeacher.UserID);
            }

            // ====================
            // Luc Student
            // ====================
            if (!await context.Users.AnyAsync(u => u.Email == "dongxuanluc2018@gmail.com"))
            {
                var lucStudent = new User(
                    Guid.NewGuid(),
                    "leninh2004@gmail.com",
                    "28012005",
                    "0000002018",
                    "Luc Student",
                    "Student account",
                    Role.Student,
                    null,
                    true
                );

                context.Users.Add(lucStudent);
                result.StudentIds.Add(lucStudent.UserID);
            }

            // ====================
            // Bulk Users (543)
            // ====================
            if (!await context.Users.AnyAsync(u => u.Email.StartsWith("student") || u.Email.StartsWith("teacher")))
            {
                var users = new List<User>();

                var endDate = DateTime.Now;
                var startDate = endDate.AddYears(-3);

                var random = new Random();

                for (int i = 1; i <= 543; i++)
                {
                    var createdAt = startDate.AddSeconds(
                        random.Next(0, (int)(endDate - startDate).TotalSeconds)
                    );

                    var isTeacher = random.NextDouble() < 0.05;
                    var role = isTeacher ? Role.Teacher : Role.Student;

                    var user = new User(
                        Guid.NewGuid(),
                        isTeacher ? $"teacher{i}@gmail.com" : $"student{i}@gmail.com",
                        "28012005",
                        $"09{random.Next(10000000, 99999999)}",
                        isTeacher ? $"Teacher {i}" : $"Student {i}",
                        $"{role} account",
                        role,
                        createdAt,
                        true
                    );

                    users.Add(user);

                    if (role == Role.Teacher)
                        result.TeacherIds.Add(user.UserID);
                    else
                        result.StudentIds.Add(user.UserID);
                }

                context.Users.AddRange(users);
            }
            else
            {
                // If already seeded, still return IDs
                result.TeacherIds = await context.Users
                    .Where(u => u.Role == Role.Teacher)
                    .Select(u => u.UserID)
                    .ToListAsync();

                result.StudentIds = await context.Users
                    .Where(u => u.Role == Role.Student)
                    .Select(u => u.UserID)
                    .ToListAsync();
            }

            return result;
        }
    }
}
