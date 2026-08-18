namespace Backend.Api.WorkflowEngine.Http
{
    public interface IPublicNetworkConnector
    {
        ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken);
    }
}
