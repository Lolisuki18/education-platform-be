using API.Models.Common;
using Application.Features.Users.Queries;
using Application.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
    }
}
