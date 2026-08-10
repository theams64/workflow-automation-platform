using Backend.Api.Models.Entities;

namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed record StepExecutionContext(
        Workflow Workflow,
        WorkflowStep Step,
        WorkflowExecutionContext WorkflowContext
    );
}
