using Backend.Api.Configuration;
using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Api.Services.Auth
{
    public sealed class RefreshTokenService : IRefreshTokenService
    {
        private const string RotationReason = "rotated";
        private const string LogoutReason = "user_logout";
        private const string ReuseReason = "reuse_detected";
        private const string SessionLimitReason = "active_session_limit";

        private readonly AppDbContext _dbContext;
        private readonly RefreshTokenOptions _options;
        private readonly TimeProvider _timeProvider;

        public RefreshTokenService(AppDbContext dbContext, IOptions<RefreshTokenOptions> options, TimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task<ServiceResult<IssuedRefreshToken>> IssueAsync(int userId, CancellationToken ct = default)
        {
            var now = _timeProvider.GetUtcNow();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            await RemoveExpiredTokensAsync(userId, now, ct);
            await EnforceSessionLimitAsync(userId, now, ct);

            var generated = GenerateToken();

            var entity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = HashToken(generated.RawToken),
                FamilyId = Guid.NewGuid(),
                CreatedAt = now,
                ExpiresAt = generated.ExpiresAtUtc
            };

            _dbContext.RefreshToken.Add(entity);

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return ServiceResult<IssuedRefreshToken>.Ok(generated);
        }

        public async Task<ServiceResult<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return InvalidRefreshToken();
            }

            var tokenHash = HashToken(rawToken);
            var now = _timeProvider.GetUtcNow();

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            var existing = await _dbContext.RefreshToken
                .AsNoTracking()
                .Include(token => token.User)
                .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, ct);

            if (existing is null)
            {
                return InvalidRefreshToken();
            }

            if (existing.RevokedAt is not null)
            {
                await RevokeActiveFamilyTokensAsync(existing.FamilyId, now, ReuseReason, ct);
                await transaction.CommitAsync(ct);

                return ReusedRefreshToken();
            }

            if (existing.ExpiresAt <= now)
            {
                await RevokeTokenIfActiveAsync(existing.Id, now, "expired", ct);
                await transaction.CommitAsync(ct);

                return InvalidRefreshToken();
            }

            var updatedRows = await _dbContext.RefreshToken
                .Where(token => token.Id == existing.Id && token.RevokedAt == null && token.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, RotationReason), ct);

            if (updatedRows != 1)
            {
                await RevokeActiveFamilyTokensAsync(existing.FamilyId, now, ReuseReason, ct);
                await transaction.CommitAsync(ct);

                return ReusedRefreshToken();
            }

            var replacement = GenerateToken();

            var replacementEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = existing.UserId,
                TokenHash = HashToken(replacement.RawToken),
                FamilyId = existing.FamilyId,
                CreatedAt = now,
                ExpiresAt = replacement.ExpiresAtUtc
            };

            _dbContext.RefreshToken.Add(replacementEntity);
            await _dbContext.SaveChangesAsync(ct);

            var linkedRows = await _dbContext.RefreshToken
                .Where(token => token.Id == existing.Id && token.ReplacedByTokenId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.ReplacedByTokenId, replacementEntity.Id), ct);

            if (linkedRows != 1)
            {
                throw new InvalidOperationException("The rotated refresh token could not be linked to its replacement.");
            }

            await transaction.CommitAsync(ct);

            return ServiceResult<RotatedRefreshToken>.Ok(
                new RotatedRefreshToken(
                    existing.User,
                    replacement.RawToken,
                    replacement.ExpiresAtUtc));
        }

        public async Task<ServiceResult<bool>> RevokeFamilyAsync(int userId, string rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                return InvalidRevocationToken();
            }

            var tokenHash = HashToken(rawToken);
            var now = _timeProvider.GetUtcNow();

            var token = await _dbContext.RefreshToken
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash && candidate.UserId == userId, ct);

            if (token is null)
            {
                return ServiceResult<bool>.Ok(true);
            }

            await RevokeActiveFamilyTokensAsync(token.FamilyId, now, LogoutReason, ct);

            return ServiceResult<bool>.Ok(true);
        }

        private IssuedRefreshToken GenerateToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(_options.TokenSizeBytes);

            var rawToken = Base64UrlEncoder.Encode(randomBytes);

            var expiresAt = _timeProvider.GetUtcNow().AddDays(_options.LifetimeDays);

            return new IssuedRefreshToken(rawToken, expiresAt);
        }

        private async Task EnforceSessionLimitAsync(int userId, DateTimeOffset now, CancellationToken ct)
        {
            var activeTokens = await _dbContext.RefreshToken
                .AsNoTracking()
                .Where(token => token.UserId == userId && token.RevokedAt == null && token.ExpiresAt > now)
                .OrderByDescending(token => token.CreatedAt)
                .Select(token => new
                {
                    token.Id,
                    token.CreatedAt
                })
                .ToListAsync(ct);

            var numberToRevoke = activeTokens.Count - _options.MaximumActiveSessionsPerUser + 1;

            if (numberToRevoke <= 0)
            {
                return;
            }

            var tokenIdsToRevoke = activeTokens
                .OrderBy(token => token.CreatedAt)
                .Take(numberToRevoke)
                .Select(token => token.Id)
                .ToArray();

            await _dbContext.RefreshToken
                .Where(token => tokenIdsToRevoke.Contains(token.Id) && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, SessionLimitReason), ct);
        }

        private async Task RemoveExpiredTokensAsync(int userId, DateTimeOffset now, CancellationToken ct)
        {
            var retentionCutoff = now.AddDays(-30);

            await _dbContext.RefreshToken
                .Where(token => token.UserId == userId && token.ExpiresAt < retentionCutoff)
                .ExecuteDeleteAsync(ct);
        }

        private async Task RevokeActiveFamilyTokensAsync(Guid familyId, DateTimeOffset now, string reason, CancellationToken ct)
        {
            await _dbContext.RefreshToken
                .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, reason), ct);
        }

        private async Task RevokeTokenIfActiveAsync(Guid tokenId, DateTimeOffset now, string reason, CancellationToken ct)
        {
            await _dbContext.RefreshToken
                .Where(token => token.Id == tokenId && token.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.RevocationReason, reason), ct);
        }

        private static string HashToken(string rawToken)
        {
            var bytes = Encoding.UTF8.GetBytes(rawToken);
            var hash = SHA256.HashData(bytes);

            return Convert.ToHexString(hash);
        }

        private static ServiceResult<RotatedRefreshToken> InvalidRefreshToken()
        {
            return ServiceResult<RotatedRefreshToken>.Fail(new ServiceError("invalid_refresh_token", "The refresh token is invalid or expired."));
        }

        private static ServiceResult<bool> InvalidRevocationToken()
        {
            return ServiceResult<bool>.Fail(new ServiceError("invalid_refresh_token", "The refresh token is invalid."));
        }

        private static ServiceResult<RotatedRefreshToken> ReusedRefreshToken()
        {
            return ServiceResult<RotatedRefreshToken>.Fail(new ServiceError("refresh_token_reused", "The refresh token is no longer valid."));
        }
    }
}
