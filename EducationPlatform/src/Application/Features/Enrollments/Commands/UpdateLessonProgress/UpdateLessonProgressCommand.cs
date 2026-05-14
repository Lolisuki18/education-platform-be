using MediatR;
using Infrastructure.Interface;

namespace Application.Features.Enrollments.Commands.UpdateLessonProgress
{
    public class UpdateLessonProgressCommand : IRequest<Unit>
    {
        public Guid EnrollmentID { get; set; }
        public Guid ChapterID { get; set; }
        public Guid LessonID { get; set; }
        public bool IsCompleted { get; set; }
        public Guid CallerId { get; set; }
    }

    public class UpdateLessonProgressCommandHandler : IRequestHandler<UpdateLessonProgressCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateLessonProgressCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(UpdateLessonProgressCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();

            await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertLessonProgress(request.EnrollmentID, request.ChapterID, request.LessonID, request.IsCompleted);

            await _unitOfWork.CommitAsync(request.CallerId.ToString());

            return Unit.Value;
        }
    }
}
