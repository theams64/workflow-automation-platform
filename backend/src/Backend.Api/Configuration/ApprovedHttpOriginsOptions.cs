namespace Backend.Api.Configuration
{
    public sealed class ApprovedHttpOriginsOptions
    {
        public const string SectionName = "ApprovedHttpOrigins";

        public Dictionary<string, ApprovedHttpOriginOptions> Origins { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ApprovedHttpOriginOptions
    {
        public bool Enabled { get; init; } = true;
        public string BaseUri { get; init; } = string.Empty;
        public List<string> AllowedMethods { get; init; } = ["GET"];
        public List<string> AllowedPathPrefixes { get; init; } = [];
        public List<string> AllowedQueryParameters { get; init; } = [];
        public List<string> SelectedResponseHeaders { get; init; } = [];
        public int? MaximumResponseBytes { get; init; }
        public int? ResponseHeadersTimeoutMilliseconds { get; init; }
        public int? BodyReadTimeoutMilliseconds { get; init; }
    }
}
