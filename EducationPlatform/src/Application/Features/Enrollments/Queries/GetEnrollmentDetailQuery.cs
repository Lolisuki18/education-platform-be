using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.BusinessException;
using Application.Interface;
using Domain.EnrollmentManagement.Aggregate;

namespace Application.Features.Enrollments.Queries.GetEnrollmentDetail
{
    public class GetEnrollmentDetailQuery : IRequest<EnrollmentDetailDTO>
    {
        public Guid EnrollmentID { get; set; }
    }

    public class GetEnrollmentDetailQueryHandler : IRequestHandler<GetEnrollmentDetailQuery, EnrollmentDetailDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetEnrollmentDetailQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<EnrollmentDetailDTO> Handle(GetEnrollmentDetailQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var enrollment = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentDetailByID(request.EnrollmentID);

            if (enrollment == null)
                throw new NotFound("Enrollment detail not found");

            // Basic authorization check
            if (enrollment.StudentID != _currentUser.Id.Value && _currentUser.Role != "Admin")
            {
                throw new ForbiddenException("You do not have permission to view this enrollment.");
            }

            var dto = _mapper.Map<EnrollmentDetailDTO>(enrollment);

            // Logic to hide quiz answers from student view
            if (dto.CourseProgress?.ChapterProgresses != null)
            {
                foreach (var chapter in dto.CourseProgress.ChapterProgresses)
                {
                    if (chapter.LessonProgresses == null) continue;

                    foreach (var lesson in chapter.LessonProgresses)
                    {
                        if (lesson.QuizProgresses == null) continue;

                        foreach (var quizProgress in lesson.QuizProgresses)
                        {
                            if (quizProgress.Quiz?.Answer != null)
                            {
                                quizProgress.Quiz.Answer.CorrectAnswers = new List<string>();
                            }
                        }
                    }
                }
            }

            return dto;
        }
    }
}
