using MediatR;
using Infrastructure.Interface;
using Application.BusinessException;

namespace Application.Features.Enrollments.Commands.SubmitQuiz
{
    public record SubmitQuizResult(bool IsCorrect, string Explanation);

    public class SubmitQuizCommand : IRequest<SubmitQuizResult>
    {
        public Guid EnrollmentID { get; set; }
        public Guid ChapterID { get; set; }
        public Guid LessonID { get; set; }
        public Guid QuizID { get; set; }
        public List<string> SelectedAnswers { get; set; } = new();
        public Guid CallerId { get; set; }
    }

    public class SubmitQuizCommandHandler : IRequestHandler<SubmitQuizCommand, SubmitQuizResult>
    {
        private readonly IUnitOfWork _unitOfWork;

        public SubmitQuizCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SubmitQuizResult> Handle(SubmitQuizCommand request, CancellationToken cancellationToken)
        {
            var enrollment = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentForUpdate(request.EnrollmentID);

            if (enrollment == null)
                throw new NotFound("Enrollment not found");

            if (enrollment.StudentID != request.CallerId)
                throw new AuthenticateException("You are not the owner of this enrollment");

            await _unitOfWork.BeginTransactionAsync();
            
            var result = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertQuizProgress(
                    request.EnrollmentID, 
                    request.ChapterID, 
                    request.LessonID, 
                    request.QuizID, 
                    request.SelectedAnswers);

            await _unitOfWork.CommitAsync(request.CallerId.ToString());

            return new SubmitQuizResult(result.isCorrect, result.explanation);
        }
    }
}
