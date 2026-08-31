namespace Backend.Api.WorkflowEngine.Connections
{
    public sealed class ConnectionSecretMaterial
    {
        public ConnectionSecretMaterial(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            Value = value;
        }

        public string Value { get; }

        public override string ToString() => "[REDACTED]";
    }
}
