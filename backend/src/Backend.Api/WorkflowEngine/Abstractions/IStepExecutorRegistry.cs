namespace Backend.Api.WorkflowEngine.Abstractions
{
    public interface IStepExecutorRegistry
    {
        bool TryGet(string stepType, out IWorkflowStepExecutor executor);
        IWorkflowStepExecutor GetRequired(string stepType);
        IReadOnlyCollection<string> RegisteredStepTypes { get; }
    }
}
