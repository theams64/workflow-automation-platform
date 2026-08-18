namespace Backend.Api.WorkflowEngine.Http
{
    public interface ISafeOutboundHttpClient
    {
        Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, OutboundRequestPolicy policy, CancellationToken cancellationToken);
    }
}
