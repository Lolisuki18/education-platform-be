using Application.Results;
using API.Models.Enrollments;
using API.Models.Common;
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
        public async Task<ActionResult<ApiResponse<ListEnrollmentsResponseDto>>> ListEnrollments()
        {
            var enrollments = await mediator.Send(new GetStudentEnrollmentsQuery());
            return Ok(ApiResponse<ListEnrollmentsResponseDto>.Success(new ListEnrollmentsResponseDto
            {
                Enrollments = enrollments
            }));
        }

        [HttpGet("{enrollmentId:guid}")]
        public async Task<ActionResult<ApiResponse<ResumeEnrollmentResponseDto>>> GetEnrollment(Guid enrollmentId)
        {
            var enrollment = await mediator.Send(new GetEnrollmentDetailQuery
            {
                EnrollmentID = enrollmentId
            });
            return Ok(ApiResponse<ResumeEnrollmentResponseDto>.Success(new ResumeEnrollmentResponseDto
            {
                Enrollment = enrollment
            }));
        }

        [HttpPost("progress/lesson")]
        public async Task<ActionResult<ApiResponse>> UpdateLessonProgress([FromBody] UpdateLessonProgressRequestDto request)
        {
            await mediator.Send(new UpdateLessonProgressCommand
            {
                EnrollmentID = request.EnrollmentId,
                ChapterID    = request.ChapterId,
                LessonID     = request.LessonId,
                IsCompleted  = request.IsCompleted
            });

            return Ok(ApiResponse.Success("Lesson progress updated successfully."));
        }

        [HttpPost("progress/quiz")]
        public async Task<ActionResult<ApiResponse<UpdateQuizProgressResponseDto>>> UpdateQuizProgress([FromBody] UpdateQuizProgressRequestDto request)
        {
            var result = await mediator.Send(new SubmitQuizCommand
            {
                EnrollmentID    = request.EnrollmentId,
                ChapterID       = request.ChapterId,
                LessonID        = request.LessonId,
                QuizID          = request.QuizId,
                SelectedAnswers = request.SelectedAnswers
            });

            return Ok(ApiResponse<UpdateQuizProgressResponseDto>.Success(new UpdateQuizProgressResponseDto
            {
                IsCorrect   = result.IsCorrect,
                Explanation = result.Explanation
            }));
        }
    }
}
