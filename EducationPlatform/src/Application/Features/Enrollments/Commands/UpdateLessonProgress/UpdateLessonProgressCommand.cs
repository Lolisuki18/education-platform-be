using MediatR;
using Domain.Common.Interfaces;
using Application.Interface;
using Application.BusinessException;

namespace Application.Features.Enrollments.Commands.UpdateLessonProgress
{
    public class UpdateLessonProgressCommand : IRequest<Unit>
    {
        public Guid EnrollmentID { get; set; }
        public Guid ChapterID { get; set; }
        public Guid LessonID { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class UpdateLessonProgressCommandHandler : IRequestHandler<UpdateLessonProgressCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public UpdateLessonProgressCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(UpdateLessonProgressCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            // Ownership check
            var enrollment = await _unitOfWork.GetRepository<IEnrollmentRepository>().GetByIdAsync(request.EnrollmentID);
            if (enrollment == null || enrollment.StudentID != _currentUser.Id.Value)
                throw new ForbiddenException("Not authorized to update this enrollment.");

            await _unitOfWork.BeginTransactionAsync();

            await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertLessonProgress(request.EnrollmentID, request.ChapterID, request.LessonID, request.IsCompleted);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            return Unit.Value;
        }
    }
}
