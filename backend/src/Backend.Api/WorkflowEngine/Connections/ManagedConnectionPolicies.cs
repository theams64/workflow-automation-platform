namespace Backend.Api.WorkflowEngine.Connections
{
    public static class ManagedConnectionPolicies
    {
        private static readonly IReadOnlyDictionary<string, ManagedConnectionPolicy> Policies = new Dictionary<string, ManagedConnectionPolicy>(StringComparer.Ordinal)
        {
            [ManagedConnectionTypes.SlackWebhook] = new(ManagedConnectionTypes.SlackWebhook, "https://hooks.slack.com", ManagedCredentialTypes.SlackWebhookUrl, ManagedCredentialPlacements.Uri)
        };

        public static bool TryGet(string? connectionType, out ManagedConnectionPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(connectionType))
            {
                policy = default!;
                return false;
            }

            return Policies.TryGetValue(connectionType.Trim().ToLowerInvariant(), out policy!);
        }
    }
}
