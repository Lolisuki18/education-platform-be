using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class SubjectRepository :
        GenericRepository<Subject>,
        ISubjectRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public SubjectRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<IEnumerable<Subject>> GetSubjectsByGrade(Guid gradeId)
        {
            // Assuming there is a relation or we just return subjects if they are linked.
            // Wait, looking at the models, if a Subject has a mapping to Grade or DefaultLesson links them?
            // Since it's requested, let's implement a simple query.
            return await context.Set<Subject>()
                .Where(s => s.DefaultLessons.Any(dl => dl.GradeID == gradeId))
                .ToListAsync();
        }

        public async Task<IEnumerable<DefaultLesson>> GetDefaultLessons(
            Guid subjectId,
            Guid gradeId)
        {
            return await context.Set<DefaultLesson>()
                .Where(x => x.SubjectID == subjectId && x.GradeID == gradeId)
                .ToListAsync();
        }

        public async Task<bool> IsInUse(Guid subjectId)
        {
            var inCourses = await context.Courses.AnyAsync(c => c.SubjectID == subjectId);
            var inDefaultLessons = await context.DefaultLessons.AnyAsync(dl => dl.SubjectID == subjectId);
            return inCourses || inDefaultLessons;
        }
        #endregion
    }
}

