using Backend.Api.Models.Entities;

namespace Backend.Api.WorkflowEngine.Connections
{
    public interface IManagedConnectionRuntimeResolver
    {
        Task<ManagedConnection?> GetOwnedAsync(Guid connectionId, int userId, CancellationToken cancellationToken);
    }
}