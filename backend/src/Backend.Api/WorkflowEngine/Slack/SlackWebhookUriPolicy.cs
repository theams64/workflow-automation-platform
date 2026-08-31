namespace Backend.Api.WorkflowEngine.Slack
{
    public static class SlackWebhookUriPolicy
    {
        public const string CanonicalOrigin = "https://hooks.slack.com";

        public static bool TryCreate(string secretValue, string expectedCanonicalOrigin, out Uri uri)
        {
            uri = default!;

            if (string.IsNullOrWhiteSpace(secretValue) || secretValue.Any(char.IsControl) || !Uri.TryCreate(secretValue, UriKind.Absolute, out var candidate))
            {
                return false;
            }

            if (!Uri.TryCreate(expectedCanonicalOrigin, UriKind.Absolute, out var expectedOrigin) ||
                expectedOrigin.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(expectedOrigin.Query) ||
                !string.IsNullOrEmpty(expectedOrigin.Fragment))
            {
                return false;
            }

            if (!string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(candidate.IdnHost, expectedOrigin.IdnHost, StringComparison.OrdinalIgnoreCase) ||
                candidate.Port != expectedOrigin.Port ||
                !string.IsNullOrEmpty(candidate.UserInfo) ||
                !string.IsNullOrEmpty(candidate.Query) ||
                !string.IsNullOrEmpty(candidate.Fragment))
            {
                return false;
            }

            var segments = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length != 4 || !string.Equals(segments[0], "services", StringComparison.Ordinal) || segments.Skip(1).Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            uri = candidate;
            return true;
        }
    }
}
