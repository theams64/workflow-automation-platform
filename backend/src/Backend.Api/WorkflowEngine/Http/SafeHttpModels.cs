using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed record SafeHttpRequest(
        int UserId,
        int WorkflowId,
        string OriginId,
        HttpMethod Method,
        string Path,
        IReadOnlyDictionary<string, string?> Query,
        int MaximumResponseBytes
    );

    public sealed record SafeHttpResponse(
        int StatusCode,
        string ContentType,
        JsonElement Body,
        IReadOnlyDictionary<string, string> SelectedHeaders,
        int ReceivedBytes
    );

    public sealed class SafeHttpException : Exception
    {
        public SafeHttpException(string code, string safeMessage) : base(safeMessage)
        {
            Code = code;
            SafeMessage = safeMessage;
        }

        public string Code { get; }
        public string SafeMessage { get; }
    }
}
