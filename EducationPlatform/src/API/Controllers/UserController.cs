using Asp.Versioning;
using API.Extensions;
using API.Models.Common;
using API.Models.Users;
using Application.Features.Users.Commands;
using Application.Features.Users.Queries;
using Application.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/user")]
    [Route("api/v{version:apiVersion}/user")]
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
            var result = await mediator.Send(new GetUserDetailsQuery(), HttpContext.RequestAborted);
            return Ok(ApiResponse<UserDTO>.Success(result));
        }

        [Authorize]
        [HttpPatch("update-profile")]
        public async Task<ActionResult<ApiResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var command = new UpdateUserDetailsCommand
            {
                Name = request.Name,
                Phone = request.Phone,
                Bio = request.Bio
            };

            var result = await mediator.Send(command);

            return Ok(ApiResponse<UserDTO>.Success(result));
        }

        /// <summary>Downloads a copy of the personal data the platform holds about the caller.</summary>
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Account)]
        [HttpGet("me/export")]
        public async Task<ActionResult<ApiResponse<PersonalDataExport>>> ExportMyData()
        {
            var result = await mediator.Send(new ExportMyDataQuery(), HttpContext.RequestAborted);

            Response.Headers.ContentDisposition = "attachment; filename=\"my-data.json\"";
            return Ok(ApiResponse<PersonalDataExport>.Success(result));
        }

        /// <summary>Erases the caller's account: personal data is removed, orders and enrollments stay anonymous. Irreversible.</summary>
        [Authorize]
        [EnableRateLimiting(RateLimitPolicies.Account)]
        [HttpDelete("me")]
        public async Task<ActionResult<ApiResponse>> DeleteMyAccount([FromBody] DeleteAccountRequestDto request)
        {
            await mediator.Send(new DeleteMyAccountCommand { Password = request.Password });
            return Ok(ApiResponse.Success("Your account has been deleted."));
        }

        [Authorize(Policy = API.Helpers.Policies.AdminOnly)]
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResponse>> DeleteUser(Guid id)
        {
            await mediator.Send(new DeleteUserCommand { UserId = id });
            return Ok(ApiResponse.Success("User deleted successfully."));
        }

        [Authorize(Policy = API.Helpers.Policies.AdminOnly)]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<UserDTO>>>> GetUsers(
            [FromQuery] Domain.IdentityManagement.Enum.Role? role,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = new GetUsersQuery { Role = role, PageIndex = pageIndex, PageSize = pageSize };
            var result = await mediator.Send(query, HttpContext.RequestAborted);
            return Ok(ApiResponse<PagedResult<UserDTO>>.Success(result));
        }

        [Authorize(Policy = API.Helpers.Policies.AdminOnly)]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<UserDTO>>> GetUserById(Guid id)
        {
            var result = await mediator.Send(new GetUserDetailsQuery { UserId = id }, HttpContext.RequestAborted);
            return Ok(ApiResponse<UserDTO>.Success(result));
        }

        [Authorize(Policy = API.Helpers.Policies.AdminOnly)]
        [HttpPut("{id:guid}/status")]
        public async Task<ActionResult<ApiResponse>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequestDto request)
        {
            await mediator.Send(new UpdateUserStatusCommand { UserId = id, IsActive = request.IsActive });
            return Ok(ApiResponse.Success("User status updated successfully."));
        }

        [Authorize(Policy = API.Helpers.Policies.AdminOnly)]
        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<ApiResponse>> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequestDto request)
        {
            await mediator.Send(new UpdateUserRoleCommand { UserId = id, Role = request.Role });
            return Ok(ApiResponse.Success("User role updated successfully."));
        }
    }
}
