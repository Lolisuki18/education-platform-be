using API.Models.Common;
using API.Models.Users;
using Application.Features.Users.Commands;
using Application.Features.Users.Queries;
using Application.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly IMediator mediator;

        public UserController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserDTO>>> GetMe()
        {
            var result = await mediator.Send(new GetUserDetailsQuery());
            return Ok(ApiResponse<UserDTO>.Success(result));
        }

        [Authorize]
        [HttpPatch("update-profile")]
        public async Task<ActionResult<ApiResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var command = new UpdateUserDetailsCommand
            {
                UserId = Guid.Empty,
                Name = request.Name,
                Phone = request.Phone,
                Bio = request.Bio
            };

            var result = await mediator.Send(command);

            return Ok(ApiResponse<UserDTO>.Success(result));
        }
    }
}
