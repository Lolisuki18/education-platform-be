using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using Application.Exceptions;

namespace API.ExceptionHandlers
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // The client went away (closed the tab, timed out): nobody reads the response, and it is not a server fault
            if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug("{Method} {Path} was cancelled by the client.", httpContext.Request.Method, httpContext.Request.Path);
                httpContext.Response.StatusCode = 499;
                return true;
            }

            var problemDetails = new ProblemDetails
            {
                Status = (int)HttpStatusCode.InternalServerError,
                Title = "Server Error",
                Detail = exception.Message,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            };

            // Custom logic for specific exceptions
            if (exception is UnauthorizedAccessException || exception is AuthenticateException)
            {
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Unauthorized";
            }
            else if (exception is ForbiddenException)
            {
                problemDetails.Status = (int)HttpStatusCode.Forbidden;
                problemDetails.Title = "Forbidden";
            }
            else if (exception is NotFoundException)
            {
                problemDetails.Status = (int)HttpStatusCode.NotFound;
                problemDetails.Title = "Not Found";
            }
            else if (exception is BadRequestException)
            {
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Bad Request";
            }
            else if (exception is ConflictException)
            {
                problemDetails.Status = (int)HttpStatusCode.Conflict;
                problemDetails.Title = "Conflict";
            }
            else if (exception is TooManyRequestsException)
            {
                problemDetails.Status = (int)HttpStatusCode.TooManyRequests;
                problemDetails.Title = "Too Many Requests";
            }
            else if (exception is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } })
            {
                // Two requests raced to create the same thing (enrollment, review, slug...) and the database let only one win
                problemDetails.Status = (int)HttpStatusCode.Conflict;
                problemDetails.Title = "Conflict";
                problemDetails.Detail = "This record already exists.";
            }
            else if (exception is DbUpdateConcurrencyException)
            {
                problemDetails.Status = (int)HttpStatusCode.Conflict;
                problemDetails.Title = "Concurrent Update";
                problemDetails.Detail = "This resource was updated by another request. Please retry.";
            }

            // Expected business outcomes (not found, conflict, ...) are not errors and need no stack trace;
            // anything else is. Messages can hold e-mail addresses, so they are masked before they reach the log.
            if (problemDetails.Status >= (int)HttpStatusCode.InternalServerError)
            {
                _logger.LogError(exception, "An unhandled exception occurred: {Message}", Application.Common.LogMask.Scrub(exception.Message));
            }
            else
            {
                _logger.LogInformation("Request rejected with {Status} {Title}: {Message}",
                    problemDetails.Status, problemDetails.Title, Application.Common.LogMask.Scrub(exception.Message));
            }

            if (problemDetails.Status == (int)HttpStatusCode.InternalServerError)
            {
                problemDetails.Detail = "An unexpected error occurred on the server. Please try again later.";
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
