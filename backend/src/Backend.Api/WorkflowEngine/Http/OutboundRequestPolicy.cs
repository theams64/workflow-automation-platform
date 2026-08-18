namespace Backend.Api.WorkflowEngine.Http
{
    public sealed record OutboundRequestPolicy(
        string OriginId,
        Uri BaseUri,
        bool Enabled,
        IReadOnlySet<string> AllowedMethods,
        IReadOnlyList<string> AllowedPathPrefixes,
        IReadOnlySet<string> AllowedQueryParameters,
        IReadOnlySet<string> SelectedResponseHeaders,
        int MaximumResponseBytes,
        int ResponseHeadersTimeoutMilliseconds,
        int BodyReadTimeoutMilliseconds
    );
}
