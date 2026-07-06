using Backend.Api.Data;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Api.Services.Auth
{
    public sealed class AuthService : IAuthService
    {
        private readonly AppDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenService _jwt;

        public AuthService(AppDbContext dbContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IJwtTokenService jwt)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _jwt = jwt;
        }

        public async Task<ServiceResult<UserProfileDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            var existing = await _userManager.FindByEmailAsync(dto.Email);

            if (existing is not null)
            {
                return ServiceResult<UserProfileDto>.Fail(new ServiceError("email_taken", "Email is already registered."));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            try
            {
                var user = new ApplicationUser
                {
                    UserName = dto.Email,
                    Email = dto.Email
                };

                var createResult = await _userManager.CreateAsync(user, dto.Password);

                if (!createResult.Succeeded)
                {
                    var errors = createResult.Errors.Select(e => new ServiceError(e.Code, e.Description));
                    return ServiceResult<UserProfileDto>.Fail(errors);
                }

                var now = DateTimeOffset.UtcNow;

                var profile = new UserProfile
                {
                    IdentityUserId = user.Id,
                    DisplayName = dto.DisplayName,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _dbContext.UserProfile.Add(profile);
                await _dbContext.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);

                return ServiceResult<UserProfileDto>.Ok(
                    new UserProfileDto
                    {
                        IdentityUserId = user.Id,
                        Email = user.Email!,
                        DisplayName = profile.DisplayName,
                        CreatedAt = profile.CreatedAt,
                        UpdatedAt = profile.UpdatedAt
                    });
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user is null)
            {
                return InvalidCredentials();
            }

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!signInResult.Succeeded)
            {
                return InvalidCredentials();
            }

            var token = _jwt.CreateAccessToken(user);

            return ServiceResult<AuthResponseDto>.Ok(
                new AuthResponseDto
                {
                    AccessToken = token,
                    ExpiresInSeconds = _jwt.AccessTokenLifetimeSeconds
                });
        }

        public async Task<ServiceResult<UserProfileDto>> GetMeAsync(ClaimsPrincipal principal, CancellationToken ct = default)
        {
            var idString = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idString, out var userId))
            {
                return ServiceResult<UserProfileDto>.Fail(new ServiceError("unauthorized", "Missing or invalid user id claim."));
            }

            var profile = await _dbContext.UserProfile
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdentityUserId == userId, ct);

            if (profile is null)
            {
                return ServiceResult<UserProfileDto>.Fail(new ServiceError("profile_missing", "User profile not found."));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user is null)
            {
                return ServiceResult<UserProfileDto>.Fail(new ServiceError("unauthorized", "User not found."));
            }

            return ServiceResult<UserProfileDto>.Ok(
                new UserProfileDto
                {
                    IdentityUserId = user.Id,
                    Email = user.Email!,
                    DisplayName = profile.DisplayName,
                    CreatedAt = profile.CreatedAt,
                    UpdatedAt = profile.UpdatedAt
                });
        }

        private static ServiceResult<AuthResponseDto> InvalidCredentials()
        {
            return ServiceResult<AuthResponseDto>.Fail(new ServiceError("invalid_credentials", "invalid_credentials"));
        }
    }
}
