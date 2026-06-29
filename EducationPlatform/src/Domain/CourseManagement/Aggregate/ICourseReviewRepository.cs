

using Domain.Common.Interfaces;
using Domain.CourseManagement.Entity;

namespace Domain.CourseManagement.Aggregate
{
    public interface ICourseReviewRepository : IGenericRepository<CourseReview>
    {
        Task AddAsync(CourseReview courseReview);
        Task<IEnumerable<CourseReview>> GetReviewsByCourseId(Guid courseId, int pageIndex, int pageSize);
        Task<bool> HasStudentReviewedCourseAsync(Guid courseId, Guid studentId);
    }
}
