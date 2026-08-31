using Backend.Api.Models.Dtos.ManagedConnection;
using Backend.Api.Services.Common;

namespace Backend.Api.Services.ManagedConnection
{
    public interface IManagedConnectionService
    {
        Task<ServiceResult<ManagedConnectionResponseDto>> CreateAsync(CreateManagedConnectionRequestDto request, CancellationToken cancellationToken = default);

        Task<ServiceResult<ManagedConnectionListResponseDto>> GetAllAsync(ManagedConnectionListRequestDto request, CancellationToken cancellationToken = default);

        Task<ServiceResult<ManagedConnectionResponseDto>> GetByIdAsync(Guid connectionId, CancellationToken cancellationToken = default);

        Task<ServiceResult<ManagedConnectionResponseDto>> UpdateAsync(Guid connectionId, UpdateManagedConnectionRequestDto request, CancellationToken cancellationToken = default);

        Task<ServiceResult<ManagedConnectionResponseDto>> RevokeAsync(Guid connectionId, CancellationToken cancellationToken = default);
    }
}