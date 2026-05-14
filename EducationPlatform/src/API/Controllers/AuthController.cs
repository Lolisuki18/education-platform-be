using System.Security.Claims;
using Application.Results;
using API.Hubs;
using API.Models.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Application.Features.Identity.Commands.Login;
using Application.Features.Identity.Commands.Register;
using Application.Features.Identity.Commands.VerifyEmail;
using Application.Features.Identity.Commands.RefreshToken;

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly IHubContext<AuthHub> hubContext;

        public AuthController(
            IMediator mediator,
            IHubContext<AuthHub> hubContext)
        {
            this.mediator = mediator;
            this.hubContext = hubContext;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            var token = await mediator.Send(new LoginCommand
            {
                Email = request.Email,
                Password = request.Password
            });

            return Ok(new LoginResponseDto
            {
                AccessToken = token.Token,
                RefreshToken = token.RefreshToken
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            await mediator.Send(new RegisterCommand
            {
                Email = request.Email,
                Password = request.Password,
                Phone = request.Phone,
                Name = request.Name,
                Bio = request.Bio,
                Role = request.Role
            });

            return Accepted();
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            await mediator.Send(new VerifyEmailCommand { Otp = request.Otp });
            return NoContent();
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<LoginResponseDto>> RefreshToken([FromBody] string refreshToken)
        {
            var token = await mediator.Send(new RefreshTokenCommand { RefreshToken = refreshToken });
            return Ok(new LoginResponseDto
            {
                AccessToken = token.Token,
                RefreshToken = token.RefreshToken
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await AuthHub.ForceLogout(hubContext, userId);
            }

            return NoContent();
        }
    }
}
