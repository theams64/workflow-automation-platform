namespace Backend.Api.WorkflowEngine.Connections
{
    public sealed record ManagedConnectionPolicy(string ConnectionType, string CanonicalOrigin, string CredentialType, string CredentialPlacement);
}
