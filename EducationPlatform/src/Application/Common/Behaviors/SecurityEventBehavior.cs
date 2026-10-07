using Application.Exceptions;
using Application.Features.Identity.Commands.ChangePassword;
using Application.Features.Identity.Commands.ForgotPassword;
using Application.Features.Identity.Commands.Login;
using Application.Features.Identity.Commands.Logout;
using Application.Features.Identity.Commands.ResetPassword;
using Application.Features.Users.Commands;
using Application.Interface;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors
{
    /// <summary>
    /// Leaves a trace of the events an operator wants to see when somebody attacks an account or an account
    /// changes hands: failed and locked-out sign-ins, password resets and changes, account deletions. Each one
    /// is a structured log line (Warning for failures) and a counter tagged <c>event</c>, so an alert can fire on
    /// a spike of <c>login_failed</c> without anybody reading logs. Everything else passes through untouched.
    /// </summary>
    public class SecurityEventBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<SecurityEventBehavior<TRequest, TResponse>> _logger;
        private readonly ICurrentUser _currentUser;

        public SecurityEventBehavior(ILogger<SecurityEventBehavior<TRequest, TResponse>> logger, ICurrentUser currentUser)
        {
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var descriptor = Describe(request);
            if (descriptor == null)
                return await next();

            try
            {
                var response = await next();

                if (descriptor.Success != null)
                    Record(descriptor.Success, failure: false, descriptor.Subject);

                return response;
            }
            catch (Exception ex) when (descriptor.Failure != null && IsRefusal(ex))
            {
                // A locked-out account is a different signal from a wrong password
                var name = ex is TooManyRequestsException && descriptor.Locked != null ? descriptor.Locked : descriptor.Failure;
                Record(name, failure: true, descriptor.Subject);
                throw;
            }
        }

        /// <summary>Only a refusal by the application counts: a database outage during a sign-in is not a failed sign-in.</summary>
        private static bool IsRefusal(Exception ex) => ex is AuthenticateException
            or ForbiddenException
            or BadRequestException
            or ConflictException
            or NotFoundException
            or TooManyRequestsException
            or Domain.Exceptions.DomainException;

        private void Record(string name, bool failure, string subject)
        {
            PlatformMetrics.SecurityEvents.Add(1, new KeyValuePair<string, object?>("event", name));

            var user = _currentUser.Id?.ToString() ?? "-";
            if (failure)
                _logger.LogWarning("Security event {SecurityEvent}: user {UserId}, subject {Subject}", name, user, subject);
            else
                _logger.LogInformation("Security event {SecurityEvent}: user {UserId}, subject {Subject}", name, user, subject);
        }

        /// <summary>The events of a request, or null when it is not security related. Subjects never hold a raw e-mail address.</summary>
        internal static SecurityEventDescriptor? Describe(object request) => request switch
        {
            LoginCommand c => new("login_succeeded", "login_failed", LogMask.Email(c.Email), "login_locked_out"),
            ForgotPasswordCommand c => new("password_reset_requested", null, LogMask.Email(c.Email)),
            ResetPasswordCommand c => new("password_reset_completed", "password_reset_failed", LogMask.Email(c.Email)),
            ChangePasswordCommand => new("password_changed", "password_change_failed", "self"),
            DeleteMyAccountCommand => new("account_deleted", "account_deletion_refused", "self"),
            DeleteUserCommand c => new("account_deleted_by_admin", "account_deletion_refused", c.UserId.ToString()),
            LogoutCommand { RefreshToken: null or "" } c => new("all_sessions_revoked", null, c.UserId.ToString()),
            _ => null
        };

        internal sealed record SecurityEventDescriptor(string? Success, string? Failure, string Subject, string? Locked = null);
    }
}
