using Asp.Versioning;
using System.Security.Claims;
using Application.Results;
using API.Extensions;
using API.Helpers;
using API.Hubs;
using API.Models.Auth;
using API.Models.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Application.Features.Identity.Commands.ChangePassword;
using Application.Features.Identity.Commands.ForgotPassword;
using Application.Features.Identity.Commands.Login;
using Application.Features.Identity.Commands.ResetPassword;
using Application.Features.Identity.Commands.Register;
using Application.Features.Identity.Commands.VerifyEmail;
using Application.Features.Identity.Commands.RefreshToken;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/auth")]
    [Route("api/v{version:apiVersion}/auth")]
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

        [EnableRateLimiting(RateLimitPolicies.Login)]
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto request)
        {
            var token = await mediator.Send(new LoginCommand
            {
                Email = request.Email,
                Password = request.Password
            });

            return Ok(ApiResponse<LoginResponseDto>.Success(new LoginResponseDto
            {
                AccessToken = token.Token,
                RefreshToken = token.RefreshToken
            }, "Login successful"));
        }

        [EnableRateLimiting(RateLimitPolicies.Register)]
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse>> Register([FromBody] RegisterRequestDto request)
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

            return Accepted(ApiResponse.Success("Registration successful. Please check your email to verify your account.", 202));
        }

        [EnableRateLimiting(RateLimitPolicies.VerifyEmail)]
        [AllowAnonymous]
        [HttpPost("verify-email")]
        public async Task<ActionResult<ApiResponse>> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            await mediator.Send(new VerifyEmailCommand
            {
                Email = request.Email,
                Otp = request.Otp
            });
            return Ok(ApiResponse.Success("Email verified successfully."));
        }

        /// <summary>Sends a 6-digit code to the e-mail address when it belongs to an account. The answer never says whether it does.</summary>
        [EnableRateLimiting(RateLimitPolicies.PasswordReset)]
        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            await mediator.Send(new ForgotPasswordCommand { Email = request.Email });
            return Accepted(ApiResponse.Success("If the email belongs to an account, a verification code has been sent.", 202));
        }

        /// <summary>Sets a new password with the code from "forgot-password". Every device is signed out.</summary>
        [EnableRateLimiting(RateLimitPolicies.VerifyEmail)]
        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            await mediator.Send(new ResetPasswordCommand
            {
                Email = request.Email,
                Otp = request.Otp,
                NewPassword = request.NewPassword
            });
            return Ok(ApiResponse.Success("Password has been reset. Please sign in with the new password."));
        }

        /// <summary>Changes the password of the signed-in user. Other devices are signed out; the response holds new tokens for this one.</summary>
        [EnableRateLimiting(RateLimitPolicies.Account)]
        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResponse<LoginResponseDto>>> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            var token = await mediator.Send(new ChangePasswordCommand
            {
                CurrentPassword = request.CurrentPassword,
                NewPassword = request.NewPassword
            });

            return Ok(ApiResponse<LoginResponseDto>.Success(new LoginResponseDto
            {
                AccessToken = token.Token,
                RefreshToken = token.RefreshToken
            }, "Password changed successfully"));
        }

        [AllowStaleRole]
        [EnableRateLimiting(RateLimitPolicies.RefreshToken)]
        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<ActionResult<ApiResponse<LoginResponseDto>>> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            var token = await mediator.Send(new RefreshTokenCommand { RefreshToken = request.RefreshToken });
            return Ok(ApiResponse<LoginResponseDto>.Success(new LoginResponseDto
            {
                AccessToken = token.Token,
                RefreshToken = token.RefreshToken
            }, "Token refreshed successfully"));
        }

        /// <summary>Signs out one device (when its refresh token is sent) or every device.</summary>
        [AllowStaleRole]
        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse>> Logout(
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] LogoutRequestDto? request = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userId, out var userGuid))
            {
                await mediator.Send(new Application.Features.Identity.Commands.Logout.LogoutCommand
                {
                    UserId = userGuid,
                    RefreshToken = request?.RefreshToken
                });

                // Kicking the SignalR connections signs the user out everywhere, so only do it for a full logout
                if (string.IsNullOrWhiteSpace(request?.RefreshToken))
                {
                    await AuthHub.ForceLogout(hubContext, userId!);
                }
            }

            return Ok(ApiResponse.Success("Logged out successfully."));
        }
    }
}
