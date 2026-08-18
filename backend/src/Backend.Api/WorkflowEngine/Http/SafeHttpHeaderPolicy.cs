namespace Backend.Api.WorkflowEngine.Http
{
    public static class SafeHttpHeaderPolicy
    {
        private static readonly HashSet<string> SelectableResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "cache-control",
            "date",
            "etag",
            "expires",
            "last-modified",
            "retry-after",
            "x-ratelimit-limit",
            "x-ratelimit-remaining",
            "x-ratelimit-reset"
        };

        public static bool IsSelectableResponseHeader(string value) => !string.IsNullOrWhiteSpace(value) && SelectableResponseHeaders.Contains(value.Trim());
    }
}
