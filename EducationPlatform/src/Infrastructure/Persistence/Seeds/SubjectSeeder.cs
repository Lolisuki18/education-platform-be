using Domain.AcademicManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class SubjectSeeder
    {
        public static async Task<Dictionary<string, Guid>> SeedAsync(EducationPlatformDBContext context)
        {
            var subjectIds = new Dictionary<string, Guid>();

            if (!await context.Subjects.AnyAsync())
            {
                var subjects = new List<Subject>();

                void AddSubject(string code, string name)
                {
                    var id = Guid.NewGuid();
                    subjectIds[code] = id;

                    subjects.Add(new Subject(id, code, name, Guid.Empty));
                }

                AddSubject("MATH", "Mathematics");
                AddSubject("VIET", "Vietnamese");
                AddSubject("ENG", "English");
                AddSubject("SCI", "Science");
                AddSubject("HIS", "History");
                AddSubject("GEO", "Geography");
                AddSubject("PHYS", "Physics");
                AddSubject("CHEM", "Chemistry");
                AddSubject("BIO", "Biology");
                AddSubject("TECH", "Technology");
                AddSubject("ART", "Art");
                AddSubject("PE", "Physical Education");
                AddSubject("CIT", "Civic Education");

                context.Subjects.AddRange(subjects);
            }
            else
            {
                var subjects = await context.Subjects.ToListAsync();
                foreach (var s in subjects)
                {
                    subjectIds[s.Code] = s.SubjectID;
                }
            }

            return subjectIds;
        }
    }
}

