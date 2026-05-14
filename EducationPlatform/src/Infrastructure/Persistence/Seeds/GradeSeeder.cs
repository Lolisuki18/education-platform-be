using Domain.AcademicManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class GradeSeeder
    {
        public static async Task<Dictionary<int, Guid>> SeedAsync(EducationPlatformDBContext context)
        {
            var gradeIds = new Dictionary<int, Guid>();

            if (!await context.Grades.AnyAsync())
            {
                var grades = new List<Grade>();

                for (int i = 1; i <= 12; i++)
                {
                    var id = Guid.NewGuid();
                    gradeIds[i] = id;

                    grades.Add(new Grade(id, $"Grade {i}"));
                }

                context.Grades.AddRange(grades);
            }
            else
            {
                var grades = await context.Grades.ToListAsync();
                foreach (var g in grades)
                {
                    var number = int.Parse(g.Name.Split(" ")[1]);
                    gradeIds[number] = g.GradeID;
                }
            }

            return gradeIds;
        }
    }
}

