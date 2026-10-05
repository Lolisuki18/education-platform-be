using Asp.Versioning;
using Application.Results;
using API.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Application.Features.Enrollments.Queries.GetEnrollmentDetail;
using Application.Features.Enrollments.Commands;
using Application.Features.Enrollments.Queries;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/enrollments")]
    [Route("api/v{version:apiVersion}/enrollments")]
    [Authorize(Roles = "Student")]
    public class EnrollmentsController : ControllerBase
    {
        private readonly IMediator mediator;

        public EnrollmentsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<EnrollmentDTO>>>> ListEnrollments([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            if (pageSize > 100) pageSize = 100;
            if (pageSize <= 0) pageSize = 10;
            if (pageIndex <= 0) pageIndex = 1;

            var result = await mediator.Send(new GetStudentEnrollmentsQuery { PageIndex = pageIndex, PageSize = pageSize });
            return Ok(ApiResponse<IEnumerable<EnrollmentDTO>>.Success(result));
        }

        [HttpGet("{EnrollmentID:guid}")]
        public async Task<ActionResult<ApiResponse<EnrollmentDetailDTO>>> GetEnrollment([FromRoute] GetEnrollmentDetailQuery query)
        {
            var result = await mediator.Send(query);
            return Ok(ApiResponse<EnrollmentDetailDTO>.Success(result));
        }

        [HttpPost("progress/lesson")]
        public async Task<ActionResult<ApiResponse>> UpdateLessonProgress([FromBody] UpdateLessonProgressCommand command)
        {
            await mediator.Send(command);
            return Ok(ApiResponse.Success("Lesson progress updated successfully."));
        }

        [HttpPost("progress/quiz")]
        public async Task<ActionResult<ApiResponse<SubmitQuizResult>>> UpdateQuizProgress([FromBody] SubmitQuizCommand command)
        {
            var result = await mediator.Send(command);
            return Ok(ApiResponse<SubmitQuizResult>.Success(result));
        }
    }
}
