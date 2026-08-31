namespace Backend.Api.WorkflowEngine.Slack
{
    public sealed record SlackWebhookRequest(int UserId, int WorkflowId, Guid ConnectionId, Uri WebhookUri, string Text);
}
