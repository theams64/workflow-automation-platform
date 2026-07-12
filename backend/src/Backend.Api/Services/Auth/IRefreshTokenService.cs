using Backend.Api.Services.Common;

namespace Backend.Api.Services.Auth
{
    public interface IRefreshTokenService
    {
        Task<ServiceResult<IssuedRefreshToken>> IssueAsync(int userId, CancellationToken ct = default);
        Task<ServiceResult<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken ct = default);
        Task<ServiceResult<bool>> RevokeFamilyAsync(int userId, string rawToken, CancellationToken ct = default);
    }
}
