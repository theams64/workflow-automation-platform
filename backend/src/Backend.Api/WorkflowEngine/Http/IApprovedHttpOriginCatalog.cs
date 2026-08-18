namespace Backend.Api.WorkflowEngine.Http
{
    public interface IApprovedHttpOriginCatalog
    {
        bool TryGet(string originId, out OutboundRequestPolicy policy);
        OutboundRequestPolicy GetRequired(string originId);
        IReadOnlyCollection<string> OriginIds { get; }
    }
}
