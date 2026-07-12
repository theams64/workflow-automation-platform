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
        private readonly IRefreshTokenService _refreshTokens;

        public AuthService(AppDbContext dbContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IJwtTokenService jwt, IRefreshTokenService refreshTokens)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _jwt = jwt;
            _refreshTokens = refreshTokens;
        }

        public async Task<ServiceResult<UserProfileDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            var normalizedEmail = dto.Email.Trim();

            var existing = await _userManager.FindByEmailAsync(normalizedEmail);

            if (existing is not null)
            {
                return ServiceResult<UserProfileDto>.Fail(new ServiceError("email_taken", "Email is already registered."));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            try
            {
                var user = new ApplicationUser
                {
                    UserName = normalizedEmail,
                    Email = normalizedEmail
                };

                var createResult = await _userManager.CreateAsync(user, dto.Password);

                if (!createResult.Succeeded)
                {
                    var errors = createResult.Errors.Select(e => new ServiceError(e.Code, e.Description));

                    return ServiceResult<UserProfileDto>.Fail(errors);
                }

                var profile = new UserProfile
                {
                    IdentityUserId = user.Id,
                    DisplayName = NormalizeOptionalValue(dto.DisplayName)
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
            var email = dto.Email.Trim();

            var user = await _userManager.FindByEmailAsync(email);

            if (user is null)
            {
                return InvalidCredentials();
            }

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!signInResult.Succeeded)
            {
                return InvalidCredentials();
            }

            var refreshResult = await _refreshTokens.IssueAsync(user.Id, ct);

            if (!refreshResult.Succeeded || refreshResult.Data is null)
            {
                return ServiceResult<AuthResponseDto>.Fail(refreshResult.Errors);
            }

            return ServiceResult<AuthResponseDto>.Ok(CreateAuthResponse(user, refreshResult.Data.RawToken, refreshResult.Data.ExpiresAtUtc));
        }

        public async Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken ct = default)
        {
            var rotationResult = await _refreshTokens.RotateAsync(dto.RefreshToken, ct);

            if (!rotationResult.Succeeded || rotationResult.Data is null)
            {
                return ServiceResult<AuthResponseDto>.Fail(rotationResult.Errors);
            }

            return ServiceResult<AuthResponseDto>.Ok(CreateAuthResponse(rotationResult.Data.User, rotationResult.Data.RawToken, rotationResult.Data.ExpiresAtUtc));
        }

        public async Task<ServiceResult<bool>> RevokeRefreshTokenAsync(ClaimsPrincipal principal, RevokeRefreshTokenRequestDto dto, CancellationToken ct = default)
        {
            var userIdResult = GetUserId(principal);

            if (!userIdResult.Succeeded || userIdResult.Data is null)
            {
                return ServiceResult<bool>.Fail(userIdResult.Errors);
            }

            return await _refreshTokens.RevokeFamilyAsync(userIdResult.Data.Value, dto.RefreshToken, ct);
        }

        public async Task<ServiceResult<UserProfileDto>> GetMeAsync(ClaimsPrincipal principal, CancellationToken ct = default)
        {
            var userIdResult = GetUserId(principal);

            if (!userIdResult.Succeeded || userIdResult.Data is null)
            {
                return ServiceResult<UserProfileDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data.Value;

            var profile = await _dbContext.UserProfile
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.IdentityUserId == userId, ct);

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

        private AuthResponseDto CreateAuthResponse(ApplicationUser user, string refreshToken, DateTimeOffset refreshExpiresAtUtc)
        {
            return new AuthResponseDto
            {
                AccessToken = _jwt.CreateAccessToken(user),
                ExpiresInSeconds = _jwt.AccessTokenLifetimeSeconds,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = refreshExpiresAtUtc
            };
        }

        private static ServiceResult<int?> GetUserId(ClaimsPrincipal principal)
        {
            var idString = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(idString, out var userId))
            {
                return ServiceResult<int?>.Fail(new ServiceError("unauthorized", "Missing or invalid user id claim."));
            }

            return ServiceResult<int?>.Ok(userId);
        }

        private static string? NormalizeOptionalValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static ServiceResult<AuthResponseDto> InvalidCredentials()
        {
            return ServiceResult<AuthResponseDto>.Fail(new ServiceError("invalid_credentials", "Invalid Credentials."));
        }
    }
}
