using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.WorkflowEngine.Connections
{
    public sealed class ManagedConnectionRuntimeResolver(AppDbContext dbContext) : IManagedConnectionRuntimeResolver
    {
        public Task<ManagedConnection?> GetOwnedAsync(Guid connectionId, int userId, CancellationToken cancellationToken) =>
            dbContext.ManagedConnection
                .AsNoTracking()
                .FirstOrDefaultAsync(connection => connection.ID == connectionId && connection.UserID == userId, cancellationToken);
    }
}