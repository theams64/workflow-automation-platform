using Backend.Api.Infrastructure.RateLimiting;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using Backend.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("auth")]
    public sealed class AuthController : ControllerBase
    {
        private const int AuthenticationRequestLimitBytes = 16 * 1024;
        
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        [EnableRateLimiting(RateLimitingPolicies.Registration)]
        [RequestSizeLimit(AuthenticationRequestLimitBytes)]
        public async Task<ActionResult<UserProfileDto>> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.RegisterAsync(dto, ct);

            if (!result.Succeeded)
            {
                return BadRequest(new { errors = result.Errors });
            }

            return CreatedAtAction(nameof(Me), result.Data);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(RateLimitingPolicies.Login)]
        [RequestSizeLimit(AuthenticationRequestLimitBytes)]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.LoginAsync(dto, ct);

            if (!result.Succeeded)
            {
                return Unauthorized(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        [EnableRateLimiting(RateLimitingPolicies.TokenRefresh)]
        [RequestSizeLimit(AuthenticationRequestLimitBytes)]
        public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshTokenRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.RefreshAsync(dto, ct);

            if (!result.Succeeded)
            {
                return Unauthorized(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [HttpPost("logout")]
        [EnableRateLimiting(RateLimitingPolicies.TokenRefresh)]
        [RequestSizeLimit(AuthenticationRequestLimitBytes)]
        public async Task<IActionResult> Logout([FromBody] RevokeRefreshTokenRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.RevokeRefreshTokenAsync(User, dto, ct);

            if (!result.Succeeded)
            {
                if (result.Errors.Any(error => error.Code == "unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return NoContent();
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
        {
            var result = await _auth.GetMeAsync(User, ct);

            if (result.Succeeded)
            {
                return Ok(result.Data);
            }

            if (result.Errors.Any(error => error.Code == "unauthorized"))
            {
                return Unauthorized(new { errors = result.Errors });
            }

            if (result.Errors.Any(error => error.Code == "profile_missing"))
            {
                return NotFound(new { errors = result.Errors });
            }

            return BadRequest(new { errors = result.Errors });
        }
    }
}
