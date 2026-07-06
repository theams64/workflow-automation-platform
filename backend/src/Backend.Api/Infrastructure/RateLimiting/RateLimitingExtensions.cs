using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Api.Infrastructure.RateLimiting
{
    public static class RateLimitingExtensions
    {
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration) 
        {
            var globalPermitLimit = GetPositiveInt(configuration, "RateLimiting:Global:PermitLimit");
            var globalWindowMinutes = GetPositiveInt(configuration, "RateLimiting:Global:WindowMinutes");

            var loginPermitLimit = GetPositiveInt(configuration, "RateLimiting:Login:PermitLimit");
            var loginWindowMinutes = GetPositiveInt(configuration, "RateLimiting:Login:WindowMinutes");
            var loginSegmentsPerWindow = GetPositiveInt(configuration, "RateLimiting:Login:SegmentsPerWindow");

            var registrationPermitLimit = GetPositiveInt(configuration, "RateLimiting:Registration:PermitLimit");
            var registrationWindowMinutes = GetPositiveInt(configuration, "RateLimiting:Registration:WindowMinutes");

            var passwordResetPermitLimit = GetPositiveInt(configuration, "RateLimiting:PasswordReset:PermitLimit");
            var passwordResetWindowMinutes = GetPositiveInt(configuration, "RateLimiting:PasswordReset:WindowMinutes");

            var tokenRefreshPermitLimit = GetPositiveInt(configuration, "RateLimiting:TokenRefresh:PermitLimit");
            var tokenRefreshWindowMinutes = GetPositiveInt(configuration, "RateLimiting:TokenRefresh:WindowMinutes");
            var tokenRefreshSegmentsPerWindow = GetPositiveInt(configuration, "RateLimiting:TokenRefresh:SegmentsPerWindow");

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Applies to every endpoint unless rate limiting is explicitly disabled
                options.GlobalLimiter =
                    PartitionedRateLimiter.Create<HttpContext, string>(
                        httpContext =>
                            RateLimitPartition.GetFixedWindowLimiter(
                                GetUserOrIpPartition(httpContext),
                                _ => new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit = globalPermitLimit,
                                    Window = TimeSpan.FromMinutes(globalWindowMinutes),
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                }));

                options.AddPolicy(
                    RateLimitingPolicies.Login,
                    httpContext =>
                        RateLimitPartition.GetSlidingWindowLimiter(
                            GetIpPartition(httpContext),
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = loginPermitLimit,
                                Window = TimeSpan.FromMinutes(loginWindowMinutes),
                                SegmentsPerWindow = loginSegmentsPerWindow,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

                options.AddPolicy(
                    RateLimitingPolicies.Registration,
                    httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            GetIpPartition(httpContext),
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = registrationPermitLimit,
                                Window = TimeSpan.FromMinutes(registrationWindowMinutes),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

                options.AddPolicy(
                    RateLimitingPolicies.PasswordReset,
                    httpContext =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            GetIpPartition(httpContext),
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = passwordResetPermitLimit,
                                Window = TimeSpan.FromMinutes(passwordResetWindowMinutes),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

                options.AddPolicy(
                    RateLimitingPolicies.TokenRefresh,
                    httpContext =>
                        RateLimitPartition.GetSlidingWindowLimiter(
                            GetUserOrIpPartition(httpContext),
                            _ => new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = tokenRefreshPermitLimit,
                                Window = TimeSpan.FromMinutes(tokenRefreshWindowMinutes),
                                SegmentsPerWindow = tokenRefreshSegmentsPerWindow,
                                QueueLimit = 0,
                                AutoReplenishment = true
                            }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    var response = context.HttpContext.Response;

                    response.StatusCode = StatusCodes.Status429TooManyRequests;

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                    }

                    await response.WriteAsJsonAsync(
                        new ProblemDetails
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests.",
                            Detail = "The request limit has been exceeded. Try again later.",
                            Type = "https://www.rfc-editor.org/rfc/rfc9110#name-429-too-many-requests",
                            Instance = context.HttpContext.Request.Path
                        },
                        cancellationToken);
                };

            });

            return services;
        }

        private static int GetPositiveInt(IConfiguration configuration, string key)
        {
            var value = configuration.GetValue<int?>(key);

            if (value is null or <= 0)
            {
                throw new InvalidOperationException($"Configuration value '{key}' must be greater than zero.");
            }

            return value.Value;
        }

        private static string GetUserOrIpPartition(HttpContext httpContext) 
        {
            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return !string.IsNullOrWhiteSpace(userId) ? $"user:{userId}" : $"ip:{GetIpPartition(httpContext)}";
        }

        private static string GetIpPartition(HttpContext httpContext) 
        {
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
