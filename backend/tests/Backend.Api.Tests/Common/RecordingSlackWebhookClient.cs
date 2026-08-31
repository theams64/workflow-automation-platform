using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Slack;

namespace Backend.Api.Tests.Common
{
    public sealed class RecordingSlackWebhookClient : ISlackWebhookClient
    {
        public List<SlackWebhookRequest> Requests { get; } = [];
        public Exception? ExceptionToThrow { get; set; }

        public Task SendAsync(SlackWebhookRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}