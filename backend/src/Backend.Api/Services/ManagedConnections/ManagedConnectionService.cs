using Backend.Api.Data;
using Backend.Api.Models.Dtos.ManagedConnection;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Backend.Api.Services.ManagedConnection;
using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Slack;
using Backend.Api.WorkflowEngine.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace Backend.Api.Services.ManagedConnections
{
    public sealed partial class ManagedConnectionService(AppDbContext dbContext, ICurrentUserService currentUserService, IConnectionSecretProvider secretProvider, IClock clock) : IManagedConnectionService
    {
        private const string NameConstraint = "IX_managed_connection_user_id_name";

        [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant)]
        private static partial Regex SecretReferencePattern();

        public async Task<ServiceResult<ManagedConnectionResponseDto>> CreateAsync(CreateManagedConnectionRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(userIdResult.Errors);
            }

            var validationErrors = ValidateCreateRequest(request);
            if (validationErrors.Count > 0)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(validationErrors);
            }

            if (!ManagedConnectionPolicies.TryGet(request.ConnectionType, out var policy))
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.type_invalid", "The managed connection type is invalid."));
            }

            ConnectionSecretMaterial secret;
            try
            {
                secret = await secretProvider.GetSecretAsync(request.SecretReference.Trim(), cancellationToken);
            }
            catch (ConnectionSecretException)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.secret_unavailable", "The managed connection secret could not be validated."));
            }

            if (policy.ConnectionType == ManagedConnectionTypes.SlackWebhook && !SlackWebhookUriPolicy.TryCreate(secret.Value, policy.CanonicalOrigin, out _))
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.secret_invalid", "The managed connection credential is invalid for the selected connection type."));
            }

            var userId = userIdResult.Data;
            var normalizedName = request.Name.Trim();

            var duplicate = await dbContext.ManagedConnection
                .AsNoTracking()
                .AnyAsync(connection => connection.UserID == userId && connection.Name == normalizedName, cancellationToken);

            if (duplicate)
            {
                return DuplicateNameFailure();
            }

            var connection = new Models.Entities.ManagedConnection
            {
                ID = Guid.NewGuid(),
                UserID = userId,
                Name = normalizedName,
                ConnectionType = policy.ConnectionType,
                CanonicalOrigin = policy.CanonicalOrigin,
                CredentialType = policy.CredentialType,
                SecretReference = request.SecretReference.Trim(),
                CredentialPlacement = policy.CredentialPlacement,
                IsEnabled = true
            };

            dbContext.ManagedConnection.Add(connection);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, NameConstraint))
            {
                return DuplicateNameFailure();
            }

            return ServiceResult<ManagedConnectionResponseDto>.Ok(Map(connection));
        }

        public async Task<ServiceResult<ManagedConnectionListResponseDto>> GetAllAsync(ManagedConnectionListRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<ManagedConnectionListResponseDto>.Fail(userIdResult.Errors);
            }

            if (request.Page < 1 || request.PageSize < 1 || request.PageSize > WorkflowLimits.MaxPageSize)
            {
                return ServiceResult<ManagedConnectionListResponseDto>.Fail(new ServiceError("pagination.invalid", "The requested page is invalid."));
            }

            var owned = dbContext.ManagedConnection
                .AsNoTracking()
                .Where(connection => connection.UserID == userIdResult.Data);

            var totalCount = await owned.CountAsync(cancellationToken);
            var skip = checked((request.Page - 1) * request.PageSize);

            var items = await owned
                .OrderBy(connection => connection.ID)
                .Skip(skip)
                .Take(request.PageSize)
                .Select(connection => new ManagedConnectionResponseDto
                {
                    Id = connection.ID,
                    Name = connection.Name,
                    ConnectionType = connection.ConnectionType,
                    CanonicalOrigin = connection.CanonicalOrigin,
                    IsEnabled = connection.IsEnabled,
                    IsRevoked = connection.RevokedAt != null,
                    RevokedAt = connection.RevokedAt,
                    CreatedAt = connection.CreatedAt,
                    UpdatedAt = connection.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<ManagedConnectionListResponseDto>.Ok(
                new ManagedConnectionListResponseDto
                {
                    Items = items,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalCount = totalCount
                });
        }

        public async Task<ServiceResult<ManagedConnectionResponseDto>> GetByIdAsync(Guid connectionId, CancellationToken cancellationToken = default)
        {
            var owned = await GetOwnedAsync(connectionId, cancellationToken);

            return owned.Succeeded ? ServiceResult<ManagedConnectionResponseDto>.Ok(Map(owned.Data!)) : ServiceResult<ManagedConnectionResponseDto>.Fail(owned.Errors);
        }

        public async Task<ServiceResult<ManagedConnectionResponseDto>> UpdateAsync(Guid connectionId, UpdateManagedConnectionRequestDto request, CancellationToken cancellationToken = default)
        {
            var owned = await GetOwnedTrackedAsync(connectionId, cancellationToken);

            if (!owned.Succeeded)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(owned.Errors);
            }

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > WorkflowLimits.ConnectionNameMaxLength || request.IsEnabled is null)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.request_invalid", "The managed connection update is invalid."));
            }

            var connection = owned.Data!;

            if (connection.RevokedAt is not null && request.IsEnabled == true)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.revoked", "A revoked managed connection cannot be re-enabled."));
            }

            connection.Name = request.Name.Trim();
            connection.IsEnabled = request.IsEnabled.Value;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, NameConstraint))
            {
                return DuplicateNameFailure();
            }

            return ServiceResult<ManagedConnectionResponseDto>.Ok(Map(connection));
        }

        public async Task<ServiceResult<ManagedConnectionResponseDto>> RevokeAsync(Guid connectionId, CancellationToken cancellationToken = default)
        {
            var owned = await GetOwnedTrackedAsync(connectionId, cancellationToken);

            if (!owned.Succeeded)
            {
                return ServiceResult<ManagedConnectionResponseDto>.Fail(owned.Errors);
            }

            var connection = owned.Data!;

            if (connection.RevokedAt is null)
            {
                connection.RevokedAt = clock.UtcNow;
                connection.IsEnabled = false;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return ServiceResult<ManagedConnectionResponseDto>.Ok(Map(connection));
        }

        private async Task<ServiceResult<Models.Entities.ManagedConnection>> GetOwnedAsync(Guid connectionId, CancellationToken cancellationToken)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<Models.Entities.ManagedConnection>.Fail(userIdResult.Errors);
            }

            var connection = await dbContext.ManagedConnection
                .AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.ID == connectionId && candidate.UserID == userIdResult.Data, cancellationToken);

            return connection is null ? ConnectionNotFoundFailure<Models.Entities.ManagedConnection>() : ServiceResult<Models.Entities.ManagedConnection>.Ok(connection);
        }

        private async Task<ServiceResult<Models.Entities.ManagedConnection>> GetOwnedTrackedAsync(Guid connectionId, CancellationToken cancellationToken)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<Models.Entities.ManagedConnection>.Fail(userIdResult.Errors);
            }

            var connection = await dbContext.ManagedConnection
                .FirstOrDefaultAsync(candidate => candidate.ID == connectionId && candidate.UserID == userIdResult.Data, cancellationToken);

            return connection is null ? ConnectionNotFoundFailure<Models.Entities.ManagedConnection>() : ServiceResult<Models.Entities.ManagedConnection>.Ok(connection);
        }

        private ServiceResult<int> TryGetCurrentUserId()
        {
            try
            {
                return ServiceResult<int>.Ok(currentUserService.GetUserId());
            }
            catch (UnauthorizedAccessException)
            {
                return ServiceResult<int>.Fail(new ServiceError("auth.unauthorized", "The current user could not be determined."));
            }
        }

        private static List<ServiceError> ValidateCreateRequest(CreateManagedConnectionRequestDto request)
        {
            var errors = new List<ServiceError>();

            if (string.IsNullOrWhiteSpace(request.Name) ||request.Name.Trim().Length > WorkflowLimits.ConnectionNameMaxLength)
            {
                errors.Add(new("connection.name_invalid", "The managed connection name is invalid."));
            }

            if (string.IsNullOrWhiteSpace(request.ConnectionType) || request.ConnectionType.Trim().Length > WorkflowLimits.ConnectionTypeMaxLength)
            {
                errors.Add(new("connection.type_invalid", "The managed connection type is invalid."));
            }

            if (string.IsNullOrWhiteSpace(request.SecretReference) || request.SecretReference.Trim().Length > WorkflowLimits.ConnectionSecretReferenceMaxLength || !SecretReferencePattern().IsMatch(request.SecretReference.Trim()))
            {
                errors.Add(new("connection.secret_reference_invalid", "The managed connection secret reference is invalid."));
            }

            return errors;
        }

        private static ManagedConnectionResponseDto Map(Models.Entities.ManagedConnection connection) => new()
        {
            Id = connection.ID,
            Name = connection.Name,
            ConnectionType = connection.ConnectionType,
            CanonicalOrigin = connection.CanonicalOrigin,
            IsEnabled = connection.IsEnabled,
            IsRevoked = connection.RevokedAt is not null,
            RevokedAt = connection.RevokedAt,
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt
        };

        private static ServiceResult<ManagedConnectionResponseDto> DuplicateNameFailure() =>
            ServiceResult<ManagedConnectionResponseDto>.Fail(new ServiceError("connection.duplicate_name", "A managed connection with this name already exists."));

        private static ServiceResult<T> ConnectionNotFoundFailure<T>() =>
            ServiceResult<T>.Fail(new ServiceError("connection.not_found", "Managed connection was not found."));

        private static bool IsUniqueConstraintViolation(DbUpdateException exception, string expectedConstraint) => 
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException && 
            string.Equals(postgresException.ConstraintName, expectedConstraint, StringComparison.OrdinalIgnoreCase);
    }
}
