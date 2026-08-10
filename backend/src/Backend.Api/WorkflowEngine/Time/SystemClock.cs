namespace Backend.Api.WorkflowEngine.Time
{
    public sealed class SystemClock(TimeProvider timeProvider) : IClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
