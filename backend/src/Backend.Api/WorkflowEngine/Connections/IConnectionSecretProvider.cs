namespace Backend.Api.WorkflowEngine.Connections
{
    public interface IConnectionSecretProvider
    {
        Task<ConnectionSecretMaterial> GetSecretAsync(string secretReference, CancellationToken cancellationToken);
    }
}
