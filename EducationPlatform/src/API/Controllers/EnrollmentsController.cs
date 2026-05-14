using Application.Results;
using API.Helper;
using API.Models.Enrollments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Application.Features.Enrollments.Queries.GetStudentEnrollments;
using Application.Features.Enrollments.Queries.GetEnrollmentDetail;
using Application.Features.Enrollments.Commands.UpdateLessonProgress;
using Application.Features.Enrollments.Commands.SubmitQuiz;

namespace API.Controllers
{
    [ApiController]
    [Route("api/enrollments")]
    [Authorize(Roles = "Student")]
    public class EnrollmentsController : ControllerBase
    {
        private readonly IMediator mediator;

        public EnrollmentsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ListEnrollmentsResponseDto>> ListEnrollments()
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);
            var enrollments = await mediator.Send(new GetStudentEnrollmentsQuery { StudentID = userId });
            return Ok(new ListEnrollmentsResponseDto
            {
                Enrollments = enrollments
            });
        }

        [HttpGet("{enrollmentId:guid}")]
        public async Task<ActionResult<ResumeEnrollmentResponseDto>> GetEnrollment(Guid enrollmentId)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);
            var enrollment = await mediator.Send(new GetEnrollmentDetailQuery 
            { 
                EnrollmentID = enrollmentId,
                CallerId = userId
            });
            return Ok(new ResumeEnrollmentResponseDto
            {
                Enrollment = enrollment
            });
        }

        [HttpPost("progress/lesson")]
        public async Task<IActionResult> UpdateLessonProgress([FromBody] UpdateLessonProgressRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            await mediator.Send(new UpdateLessonProgressCommand
            {
                EnrollmentID = request.EnrollmentId,
                ChapterID    = request.ChapterId,
                LessonID     = request.LessonId,
                IsCompleted  = request.IsCompleted,
                CallerId     = userId
            });

            return Ok();
        }

        [HttpPost("progress/quiz")]
        public async Task<ActionResult<UpdateQuizProgressResponseDto>> UpdateQuizProgress([FromBody] UpdateQuizProgressRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            var result = await mediator.Send(new SubmitQuizCommand
            {
                EnrollmentID    = request.EnrollmentId,
                ChapterID       = request.ChapterId,
                LessonID        = request.LessonId,
                QuizID          = request.QuizId,
                SelectedAnswers = request.SelectedAnswers,
                CallerId        = userId
            });

            return Ok(new UpdateQuizProgressResponseDto
            {
                IsCorrect = result.IsCorrect,
                Explanation = result.Explanation
            });
        }
    }
}
