
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class CourseReviewRepository : GenericRepository<CourseReview>,
        ICourseReviewRepository
    {
        public CourseReviewRepository(EducationPlatformDBContext context) : base(context)
        {
        }

        public async Task AddAsync(CourseReview courseReview)
        {
            await context.AddAsync(courseReview);
        }
        public async Task<IEnumerable<CourseReview>> GetReviewsByCourseId(Guid courseId, int pageIndex, int pageSize)
        {
            return await context.CourseReviews
                .Include(r => r.Course)
                .Include(r => r.Student)
                .Where(r => r.CourseID == courseId && r.DeleteAt == null)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}

