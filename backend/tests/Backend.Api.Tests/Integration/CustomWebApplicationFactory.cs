using Backend.Api.Data;
using Backend.Api.Infrastructure.RateLimiting;
using Backend.Api.Services.Workflow;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Abstractions;
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
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddJsonFile(
                    Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"),
                    optional: false,
                    reloadOnChange: false);
            });

            builder.ConfigureServices(services =>
            {
                using var serviceProvider = services.BuildServiceProvider();
                using var scope = serviceProvider.CreateScope();

                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing from appsettings.Test.json.");
                var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing from appsettings.Test.json.");
                var key = configuration["Jwt:Key"]?? throw new InvalidOperationException( "Jwt:Key is missing from appsettings.Test.json.");

                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.Zero
                    };
                });

                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                db.Database.EnsureDeleted();
                db.Database.Migrate();
            });

            builder.ConfigureTestServices(services =>
            {
                // Remove the rate-limiter configuration registered by Program.cs.
                services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
                services.RemoveAll<IPostConfigureOptions<RateLimiterOptions>>();

                services.AddScoped<IWorkflowStepExecutor>(_ => new FakeStepExecutor("http"));
                services.AddScoped<IWorkflowStepExecutor>(_ => new FakeStepExecutor("email"));
                services.AddScoped<IWorkflowStepExecutor>(_ => new FakeStepExecutor("delay"));

                // Register permissive policies for normal integration tests.
                services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext => RateLimitPartition.GetNoLimiter(partitionKey: "integration-tests"));

                    options.AddPolicy(RateLimitingPolicies.Login, httpContext => RateLimitPartition.GetNoLimiter(partitionKey: "integration-tests"));
                    options.AddPolicy(RateLimitingPolicies.Registration, httpContext => RateLimitPartition.GetNoLimiter(partitionKey: "integration-tests"));
                    options.AddPolicy(RateLimitingPolicies.PasswordReset, httpContext => RateLimitPartition.GetNoLimiter(partitionKey: "integration-tests"));
                    options.AddPolicy(RateLimitingPolicies.TokenRefresh, httpContext => RateLimitPartition.GetNoLimiter(partitionKey: "integration-tests"));
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
