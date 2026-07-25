using Application.BusinessException;
using Domain.CourseManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;
using Application.Interface;

namespace Application.Features.Courses.ReviewCourse
{

    public class ReviewCourseCommandHandler : IRequestHandler<ReviewCourseCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public ReviewCourseCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(ReviewCourseCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            // ---------- 1. Load the Aggregate ----------
            var course = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetCourseDetailByID(request.CourseID);

            if (course == null)
                throw new NotFound($"Course with ID: {request.CourseID} is not found");

            // ---------- 2. Convert DTO tuples for domain method ----------
            var violatedChapters = request.ViolatedChapters?
                .Select(x => (x.ViolatedChapterId, x.AdminNote))
                .ToList();

            // ---------- 3. Delegate ALL business logic to the Domain Aggregate ----------
            var violatedPolicies = course.ReviewCourse(
                request.ViolatedPolicyIDs,
                violatedChapters,
                request.AdminNote,
                _currentUser.Id.Value);

            // ---------- 4. Persist ----------
            await _unitOfWork.BeginTransactionAsync();

            _unitOfWork.GetRepository<ICourseRepository>()
                       .Update(course.CourseID, course);

            _unitOfWork.GetRepository<ICourseRepository>()
                       .ReplaceViolatedPolicies(course.CourseID, violatedPolicies);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
