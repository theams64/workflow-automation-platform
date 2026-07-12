using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using Backend.Api.Services.Common;
using System.Security.Claims;

namespace Backend.Api.Services.Auth
{
    public interface IAuthService
    {
        Task<ServiceResult<UserProfileDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken ct =  default);
        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
        Task<ServiceResult<AuthResponseDto>> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
        Task<ServiceResult<bool>> RevokeRefreshTokenAsync(ClaimsPrincipal principal, RevokeRefreshTokenRequestDto dto, CancellationToken ct = default);
        Task<ServiceResult<UserProfileDto>> GetMeAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    }
}
