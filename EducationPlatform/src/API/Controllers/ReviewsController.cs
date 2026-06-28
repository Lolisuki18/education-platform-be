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
    [Route("api/courses/{courseId:guid}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ReviewsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CourseReviewDTO>>> CreateReview(Guid courseId, [FromBody] CreateReviewCommand command)
        {
            command.CourseId = courseId;
            var result = await _mediator.Send(command);
            return Ok(ApiResponse<CourseReviewDTO>.Success(result, "Review submitted successfully."));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<CourseReviewDTO>>>> GetCourseReviews(Guid courseId, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _mediator.Send(new GetCourseReviewsQuery
            {
                CourseId = courseId,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
            return Ok(ApiResponse<IEnumerable<CourseReviewDTO>>.Success(result));
        }
    }
}
