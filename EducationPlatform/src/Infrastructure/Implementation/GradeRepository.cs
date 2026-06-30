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
        public async Task<bool> IsInUse(Guid gradeId)
        {
            var inCourses = await context.Courses.AnyAsync(c => c.GradeID == gradeId);
            var inDefaultLessons = await context.DefaultLessons.AnyAsync(dl => dl.GradeID == gradeId);
            return inCourses || inDefaultLessons;
        }
        #endregion
    }
}

