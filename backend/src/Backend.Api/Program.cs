using Backend.Api.Configuration;
using Backend.Api.Data;
using Backend.Api.Infrastructure.Errors;
using Backend.Api.Infrastructure.RateLimiting;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Auth;
using Backend.Api.Services.Common;
using Backend.Api.Services.ManagedConnection;
using Backend.Api.Services.ManagedConnections;
using Backend.Api.Services.Workflow;
using Backend.Api.Services.WorkflowExecution;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Composition;
using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Http;
using Backend.Api.WorkflowEngine.Http.Level1;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Registry;
using Backend.Api.WorkflowEngine.Slack;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Transform;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

// Controllers
builder.Services.AddControllers();

// Prevent unexpectedly large request bodies
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024;
});

// Database
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(
        options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// JWT configuration validation
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Key) &&
            Encoding.UTF8.GetByteCount(options.Key) >= 32,
        "Jwt:Key must contain at least 32 UTF-8 bytes.")
    .Validate(
        options => 
            Uri.TryCreate(options.Issuer, UriKind.Absolute, out _), 
        "Jwt:Issuer must be a valid absolute URI.")
    .Validate(
        options => 
            !string.IsNullOrWhiteSpace(options.Audience), 
        "Jwt:Audience is required.")
    .ValidateOnStart();

// Refresh-token configuration validation
builder.Services
    .AddOptions<RefreshTokenOptions>()
    .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Workflow Execution configuration
builder.Services
    .AddOptions<WorkflowExecutionOptions>()
    .Bind(configuration.GetSection(WorkflowExecutionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.WorkflowTimeoutSeconds >= options.StepTimeoutSeconds, "Workflow timeout must be greater than or equal to step timeout.")
    .ValidateOnStart();

builder.Services
    .AddOptions<SafeHttpOptions>()
    .Bind(configuration.GetSection(SafeHttpOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxCompressedResponseBytes <= WorkflowLimits.HttpMaximumCompressedResponseBytes, "SafeHttp compressed-response limit is invalid.")
    .Validate(options => options.MaxDecompressedResponseBytes <= WorkflowLimits.NormalizedOutputMaxBytes - (32 * 1024), "SafeHttp decompressed responses must leave room for the normalized output envelope.")
    .Validate(
        options =>
            options.PerUserConcurrencyLimit <= options.GlobalConcurrencyLimit &&
            options.PerWorkflowConcurrencyLimit <= options.GlobalConcurrencyLimit &&
            options.PerOriginConcurrencyLimit <= options.GlobalConcurrencyLimit,
        "SafeHttp concurrency limits are inconsistent.")
    .ValidateOnStart();

builder.Services.AddSingleton<IValidateOptions<ApprovedHttpOriginsOptions>, ApprovedHttpOriginsOptionsValidator>();

builder.Services
    .AddOptions<ApprovedHttpOriginsOptions>()
    .Bind(configuration.GetSection(ApprovedHttpOriginsOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<WorkflowExpressionOptions>()
    .Bind(configuration.GetSection(WorkflowExpressionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TransformOptions>()
    .Bind(configuration.GetSection(TransformOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<MessageCompositionOptions>()
    .Bind(configuration.GetSection(MessageCompositionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<SlackDeliveryOptions>()
    .Bind(configuration.GetSection(SlackDeliveryOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxMessageBytes <= WorkflowLimits.CompositionMaximumMessageBytes, "Slack message limits cannot exceed composition message limits.")
    .ValidateOnStart();

// JWT Authentication
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>(
        (bearerOptions, jwtOptionsAccessor) =>
        {
            var jwtOptions = jwtOptionsAccessor.Value;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));

            bearerOptions.RequireHttpsMetadata = true;
            bearerOptions.MapInboundClaims = false;
            bearerOptions.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,

                    ValidateLifetime = true,
                    RequireExpirationTime = true,

                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = ClaimTypes.Name,

                    RoleClaimType = ClaimTypes.Role
                };
        });

// Require authorization by default
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Central API error handling
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Request rate limiting
builder.Services.AddApiRateLimiting(configuration);

// Application Services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITimezoneValidator, TimezoneValidator>();
builder.Services.AddSingleton<IExecutionDateResolver, ExecutionDateResolver>();
builder.Services.AddSingleton<IDateRangeResolver, DateRangeResolver>();
builder.Services.AddSingleton<IWorkflowReferenceParser, WorkflowReferenceParser>();
builder.Services.AddSingleton<IWorkflowReferenceResolver, WorkflowReferenceResolver>();

builder.Services.AddSingleton<IApprovedHttpOriginCatalog, ApprovedHttpOriginCatalog>();
builder.Services.AddSingleton<IPublicNetworkConnector, PublicNetworkConnector>();
builder.Services.AddSingleton<IOutboundConcurrencyLimiter, OutboundConcurrencyLimiter>();
builder.Services.AddSingleton<ISafeOutboundHttpClient, SafeOutboundHttpClient>();

builder.Services.AddSingleton<WorkflowExpressionEngine>();
builder.Services.AddSingleton<TransformTemplateProcessor>();
builder.Services.AddSingleton<IConnectionSecretProvider, ConfigurationConnectionSecretProvider>();
builder.Services.AddSingleton<ISlackWebhookClient, SlackWebhookClient>();

// Scoped Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddScoped<ICronExpressionValidator, CronExpressionValidator>();
builder.Services.AddScoped<IJsonValidationHelper, JsonValidationHelper>();

builder.Services.AddScoped<IWorkflowService, WorkflowService>();

builder.Services.AddScoped<IStepExecutorRegistry, StepExecutorRegistry>();
builder.Services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();
builder.Services.AddScoped<IWorkflowRunner, WorkflowRunner>();
builder.Services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();

builder.Services.AddScoped<Level1HttpRequestMaterializer>();
builder.Services.AddScoped<IWorkflowStepExecutor, Level1HttpStepExecutor>();
builder.Services.AddScoped<IWorkflowStepExecutor, TransformStepExecutor>();
builder.Services.AddScoped<IWorkflowStepExecutor, MessageCompositionStepExecutor>();
builder.Services.AddScoped<IWorkflowStepExecutor, SlackNotificationStepExecutor>();
builder.Services.AddScoped<IManagedConnectionService, ManagedConnectionService>();
builder.Services.AddScoped<IManagedConnectionRuntimeResolver, ManagedConnectionRuntimeResolver>();

// HSTS
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(180);
    options.IncludeSubDomains = true;
    options.Preload = false;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options => 
{
    options.SwaggerDoc("v1", 
        new OpenApiInfo 
        { 
            Title = "Developer Automation Platform API", 
            Version = "v1" 
        });

    options.AddSecurityDefinition("bearer", 
        new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "JWT Bearer auth. Paste ONLY the token value (no 'Bearer ' prefix)."
        });

    options.AddSecurityRequirement(document => 
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearer", document)] = []
        });
});

var app = builder.Build();

// Centralized exception handling
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Authentication, rate limiting, and authorization
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

//app.MapGet("/", () => "Hello World!"); // Testing

app.Run();

public partial class Program { }
