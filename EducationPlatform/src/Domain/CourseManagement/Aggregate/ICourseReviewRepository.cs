

using Domain.Common.Interfaces;
using Domain.CourseManagement.Entity;

namespace Domain.CourseManagement.Aggregate
{
    public interface ICourseReviewRepository : IGenericRepository<CourseReview>
    {
        Task AddAsync(CourseReview courseReview, CancellationToken cancellationToken = default);
        Task<IEnumerable<CourseReview>> GetReviewsByCourseId(Guid courseId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
        Task<bool> HasStudentReviewedCourseAsync(Guid courseId, Guid studentId, CancellationToken cancellationToken = default);
    }
}
