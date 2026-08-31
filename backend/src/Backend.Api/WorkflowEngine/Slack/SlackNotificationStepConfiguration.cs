namespace Backend.Api.WorkflowEngine.Slack
{
    public sealed class SlackNotificationStepConfiguration
    {
        public Guid ConnectionId { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
