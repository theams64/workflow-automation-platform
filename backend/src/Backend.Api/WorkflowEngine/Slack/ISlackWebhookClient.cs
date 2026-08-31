namespace Backend.Api.WorkflowEngine.Slack
{
    public interface ISlackWebhookClient
    {
        Task SendAsync(SlackWebhookRequest request, CancellationToken cancellationToken);
    }
}