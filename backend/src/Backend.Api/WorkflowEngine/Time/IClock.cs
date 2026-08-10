namespace Backend.Api.WorkflowEngine.Time
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
