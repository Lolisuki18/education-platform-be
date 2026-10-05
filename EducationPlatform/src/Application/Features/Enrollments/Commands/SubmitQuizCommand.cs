using MediatR;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Application.Interface;
using Domain.EnrollmentManagement.Aggregate;

namespace Application.Features.Enrollments.Commands
{
    public record SubmitQuizResult(bool IsCorrect, string Explanation);

    public class SubmitQuizCommand : IRequest<SubmitQuizResult>
    {
        public Guid EnrollmentID { get; set; }
        public Guid ChapterID { get; set; }
        public Guid LessonID { get; set; }
        public Guid QuizID { get; set; }
        public List<string> SelectedAnswers { get; set; } = new();
    }

    public class SubmitQuizCommandHandler : IRequestHandler<SubmitQuizCommand, SubmitQuizResult>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public SubmitQuizCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<SubmitQuizResult> Handle(SubmitQuizCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var enrollment = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentForUpdate(request.EnrollmentID);

            if (enrollment == null)
                throw new NotFoundException("Enrollment not found");

            if (enrollment.StudentID != _currentUser.Id.Value)
                throw new ForbiddenException("You are not the owner of this enrollment");

            await _unitOfWork.BeginTransactionAsync();

            var result = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertQuizProgress(
                    request.EnrollmentID,
                    request.ChapterID,
                    request.LessonID,
                    request.QuizID,
                    request.SelectedAnswers);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            return new SubmitQuizResult(result.isCorrect, result.explanation);
        }
    }
}
