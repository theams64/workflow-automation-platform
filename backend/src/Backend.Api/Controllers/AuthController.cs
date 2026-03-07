using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using Backend.Api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) => _auth = auth;

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<UserProfileDto>> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.RegisterAsync(dto, ct);
            if (!result.Succeeded)
            {
                return base.BadRequest(new { errors = result.Errors });
            }

            return base.CreatedAtAction(nameof(Me), result.Data);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
        {
            var result = await _auth.LoginAsync(dto, ct);
            if (!result.Succeeded)
            {
                return base.Unauthorized(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpGet("me")]
        public async Task<ActionResult<UserProfileDto>> Me(CancellationToken ct)
        {
            var result = await _auth.GetMeAsync(User, ct);
            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }
            }

            return base.Ok(result.Data);
        }
    }
}
