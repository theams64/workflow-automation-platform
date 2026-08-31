using Backend.Api.Models.Validation;
using System.Text.RegularExpressions;

namespace Backend.Api.WorkflowEngine.Connections
{
    public sealed partial class ConfigurationConnectionSecretProvider(IConfiguration configuration) : IConnectionSecretProvider
    {
        [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant)]
        private static partial Regex SecretReferencePattern();

        public Task<ConnectionSecretMaterial> GetSecretAsync(string secretReference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(secretReference) || secretReference.Length > WorkflowLimits.ConnectionSecretReferenceMaxLength || !SecretReferencePattern().IsMatch(secretReference))
            {
                throw new ConnectionSecretException();
            }

            var value = configuration[$"ConnectionSecrets:{secretReference}"];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConnectionSecretException();
            }

            return Task.FromResult(new ConnectionSecretMaterial(value));
        }
    }
}