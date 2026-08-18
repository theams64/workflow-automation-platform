namespace Backend.Api.WorkflowEngine.Http
{
    public interface IOutboundConcurrencyLimiter
    {
        ValueTask<IAsyncDisposable> AcquireAsync(SafeHttpRequest request, CancellationToken cancellationToken);
    }
}
