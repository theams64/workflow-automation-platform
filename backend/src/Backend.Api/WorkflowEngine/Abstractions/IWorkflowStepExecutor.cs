using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Validation;

namespace Backend.Api.WorkflowEngine.Abstractions
{
    public interface IWorkflowStepExecutor
    {
        string StepType { get; }
        StepOutputSchema OutputSchema { get; }

        IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context);

        StepOutputSchema GetOutputSchema(string configJson);

        Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken);
    }
}
