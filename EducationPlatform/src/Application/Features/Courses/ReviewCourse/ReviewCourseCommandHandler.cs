using Application.BusinessException;
using Domain.CourseManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;
using Application.Interface;

namespace Application.Features.Courses.ReviewCourse
{
    /// <summary>
    /// Handler for ReviewCourseCommand.
    ///
    /// Architecture notes (CQRS + DDD):
    ///   1. Load Aggregate via ICourseRepository (NOT AsNoTracking — we need EF to track changes).
    ///   2. Call the Domain method Course.ReviewCourse(…) — all invariant checks live inside the Entity.
    ///   3. Persist via IUnitOfWork.CommitAsync() — the DomainEventDispatcherInterceptor will
    ///      automatically pick up any domain events raised during step 2 and dispatch them via MediatR.
    ///
    /// This Handler intentionally contains zero business logic — it is a pure orchestrator.
    /// </summary>
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
            // Course.ReviewCourse() enforces invariants, updates status (Published/Rejected),
            // stamps timestamps, and raises a domain event — zero if/else in this Handler.
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

            // CommitAsync triggers DomainEventDispatcherInterceptor → dispatches CourseReviewedEvent
            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
