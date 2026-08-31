using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Slack;

namespace Backend.Api.Tests.Common
{
    public sealed class DictionaryConnectionSecretProvider(IReadOnlyDictionary<string, string> values) : IConnectionSecretProvider
    {
        public Task<ConnectionSecretMaterial> GetSecretAsync(string secretReference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!values.TryGetValue(secretReference, out var value))
            {
                throw new ConnectionSecretException();
            }

            return Task.FromResult(new ConnectionSecretMaterial(value));
        }
    }
}