using Backend.Api.Data;
using Backend.Api.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

namespace Backend.Api.Tests.Integration
{
    public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        public const string TestIssuer = "https://localhost";
        public const string TestAudience = "TestAudience";
        public const string TestKey = "ThisIsATestJwtKeyThatIsLongEnough123!";
        public const string TestAccessTokenMinutes = "10";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                var config = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Port=5433;Database=test-workflow-automation-platform;Username=test_wap_user;Password=test_wap_dev_password",
                    ["Jwt:Issuer"] = TestIssuer,
                    ["Jwt:Audience"] = TestAudience,
                    ["Jwt:Key"] = TestKey,
                    ["Jwt:AccessTokenMinutes"] = TestAccessTokenMinutes
                };

                configBuilder.AddInMemoryCollection(config);
            });

            builder.ConfigureServices(services =>
            {
                // Force JwtBearer middleware to validate with same exact values
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = TestIssuer,
                        ValidAudience = TestAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                });

                var sp = services.BuildServiceProvider();

                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureDeleted();
                db.Database.Migrate();
            });

            builder.ConfigureTestServices(services =>
            {
                // Remove the rate-limiter configuration registered by Program.cs.
                services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
                services.RemoveAll<IPostConfigureOptions<RateLimiterOptions>>();

                // Register permissive policies for normal integration tests.
                services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode =
                        StatusCodes.Status429TooManyRequests;

                    options.GlobalLimiter =
                        PartitionedRateLimiter.Create<HttpContext, string>(
                            httpContext =>
                                RateLimitPartition.GetNoLimiter(
                                    partitionKey: "integration-tests"));

                    options.AddPolicy(
                        RateLimitingPolicies.Login,
                        httpContext =>
                            RateLimitPartition.GetNoLimiter(
                                partitionKey: "integration-tests"));

                    options.AddPolicy(
                        RateLimitingPolicies.Registration,
                        httpContext =>
                            RateLimitPartition.GetNoLimiter(
                                partitionKey: "integration-tests"));

                    options.AddPolicy(
                        RateLimitingPolicies.PasswordReset,
                        httpContext =>
                            RateLimitPartition.GetNoLimiter(
                                partitionKey: "integration-tests"));

                    options.AddPolicy(
                        RateLimitingPolicies.TokenRefresh,
                        httpContext =>
                            RateLimitPartition.GetNoLimiter(
                                partitionKey: "integration-tests"));
                });
            });
        }

        public async Task ResetDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }
}
