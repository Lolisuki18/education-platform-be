using System.Security.Claims;
using Application.Results;
using Application.Interface;
using Application.Commands.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using API.Hubs;
using API.Models.Auth;

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IIdentityService identityService;
        private readonly IHubContext<AuthHub> hubContext;

        public AuthController(
            IIdentityService identityService,
            IHubContext<AuthHub> hubContext)
        {
            this.identityService = identityService;
            this.hubContext = hubContext;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request)
        {
            var token = await identityService.Login(new LoginDto
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
            await identityService.Register(new RegisterDto
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
            await identityService.VerifyEmail(request.Otp);
            return NoContent();
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
