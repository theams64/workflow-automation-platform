namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed record WorkflowRunResult(
        Guid ExecutionId,
        string Status,
        string? ErrorCode,
        string? ErrorMessage
    );
}
