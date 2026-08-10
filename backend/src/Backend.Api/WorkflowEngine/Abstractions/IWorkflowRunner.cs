using Backend.Api.WorkflowEngine.Execution;

namespace Backend.Api.WorkflowEngine.Abstractions
{
    public interface IWorkflowRunner
    {
        Task<WorkflowRunResult> RunAsync(WorkflowRunRequest request, CancellationToken cancellationToken);
    }
}
