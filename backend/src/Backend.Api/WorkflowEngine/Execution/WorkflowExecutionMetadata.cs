namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed record WorkflowExecutionMetadata(
        Guid ExecutionId, 
        string TriggerType, 
        DateTimeOffset StartedAt, 
        DateTimeOffset? ScheduledFor, 
        DateOnly EffectiveDate, 
        string Timezone
    );
}
