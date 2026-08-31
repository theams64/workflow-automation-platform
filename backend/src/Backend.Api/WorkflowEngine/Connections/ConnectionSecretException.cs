namespace Backend.Api.WorkflowEngine.Connections
{
    public sealed class ConnectionSecretException : Exception
    {
        public ConnectionSecretException() : base("The managed connection secret could not be materialized.") 
        {
        }
    }
}
