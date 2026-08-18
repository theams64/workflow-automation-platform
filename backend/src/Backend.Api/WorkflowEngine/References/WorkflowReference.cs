namespace Backend.Api.WorkflowEngine.References
{
    public enum WorkflowReferenceScope
    {
        WorkflowInputs,
        Execution,
        StepOutput,
        CurrentItem
    }

    public sealed record WorkflowReference(WorkflowReferenceScope Scope, string? StepKey, string Path);
}
