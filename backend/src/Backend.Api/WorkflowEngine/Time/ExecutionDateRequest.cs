namespace Backend.Api.WorkflowEngine.Time
{
    public sealed record ExecutionDateRequest(
        string Timezone,
        DateOnly? ExplicitDate,
        DateTimeOffset? ScheduledFor
    );
}
