using Asp.Versioning;
using Application.Results;
using Application.Features.StudentReview.Command;
using Application.Features.StudentReview.Queries;
using API.Models.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/courses/{courseId:guid}/reviews")]
    [Route("api/v{version:apiVersion}/courses/{courseId:guid}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IMediator mediator;

        public ReviewsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CourseReviewDTO>>> CreateReview(Guid courseId, [FromBody] CreateReviewCommand command)
        {
            command.CourseId = courseId;
            var result = await mediator.Send(command);
            return Ok(ApiResponse<CourseReviewDTO>.Success(result, "Review submitted successfully."));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<CourseReviewDTO>>>> GetCourseReviews(Guid courseId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await mediator.Send(new GetCourseReviewsQuery
            {
                CourseId = courseId,
                PageIndex = pageIndex,
                PageSize = pageSize
            }, HttpContext.RequestAborted);
            return Ok(ApiResponse<IEnumerable<CourseReviewDTO>>.Success(result));
        }
    }
}
