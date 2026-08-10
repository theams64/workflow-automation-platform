using Backend.Api.Models.Entities;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed record WorkflowRunRequest(
        Workflow Workflow,
        IReadOnlyList<WorkflowStep> OrderedSteps,
        WorkflowExecution ExecutionRecord,
        JsonElement WorkflowInputs
    );
}
