namespace Backend.Api.WorkflowEngine.Time
{
    public interface IExecutionDateResolver
    {
        DateOnly Resolve(ExecutionDateRequest request);
    }
}
