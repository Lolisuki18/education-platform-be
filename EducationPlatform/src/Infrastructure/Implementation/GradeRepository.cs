using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.AcademicManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class GradeRepository :
        GenericRepository<Grade>,
        IGradeRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public GradeRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<bool> IsInUse(Guid gradeId, CancellationToken cancellationToken = default)
        {
            var inCourses = await context.Courses.AnyAsync(c => c.GradeID == gradeId, cancellationToken);
            var inDefaultLessons = await context.DefaultLessons.AnyAsync(dl => dl.GradeID == gradeId, cancellationToken);
            return inCourses || inDefaultLessons;
        }

        public async Task<bool> NameExistsAsync(string name, Guid? excludeGradeId = null, CancellationToken cancellationToken = default)
        {
            var lowered = name.ToLower();

            return await context.Grades
                .AsNoTracking()
                .AnyAsync(g => g.Name.ToLower() == lowered && (excludeGradeId == null || g.GradeID != excludeGradeId), cancellationToken);
        }
        #endregion
    }
}

