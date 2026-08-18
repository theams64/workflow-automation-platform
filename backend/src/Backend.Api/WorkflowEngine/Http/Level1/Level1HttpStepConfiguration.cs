namespace Backend.Api.WorkflowEngine.Http.Level1
{
    public sealed class Level1HttpStepConfiguration
    {
        public string OriginId { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public Dictionary<string, string?> Query { get; init; } = new(StringComparer.Ordinal);
        public int? MaximumResponseBytes { get; init; }
    }
}
