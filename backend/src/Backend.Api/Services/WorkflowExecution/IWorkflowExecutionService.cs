using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using System.Text.Json;

namespace Backend.Api.Services.WorkflowExecution
{
    public interface IWorkflowExecutionService
    {
        Task<ServiceResult<WorkflowRunResult>> ExecuteAsync(int workflowId, string triggerType, JsonElement workflowInputs, DateOnly? explicitDate = null, DateTimeOffset? scheduledFor = null, CancellationToken cancellationToken = default);
    }
}
