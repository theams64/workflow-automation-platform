using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Infrastructure.Errors
{
    public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var traceId = httpContext.TraceIdentifier;

            logger.LogError(
                exception, 
                "Unhandled exception while processing {Method} {Path}. TraceId: {TraceId}", 
                httpContext.Request.Method, 
                httpContext.Request.Path,
                traceId);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = environment.IsDevelopment() ? exception.Message : "The server encountered an unexpected error.",
                Type = "https://www.rfc-editor.org/rfc/rfc9110#name-500-internal-server-error",
                Instance = httpContext.Request.Path
            };

            problemDetails.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            return await problemDetailsService.TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = problemDetails,
                    Exception = exception
                });
        }
    }
}
