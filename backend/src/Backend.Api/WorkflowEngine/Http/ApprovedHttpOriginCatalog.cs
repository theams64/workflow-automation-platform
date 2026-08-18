using Backend.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed class ApprovedHttpOriginCatalog : IApprovedHttpOriginCatalog
    {
        private readonly IReadOnlyDictionary<string, OutboundRequestPolicy> _policies;

        public ApprovedHttpOriginCatalog(IOptions<ApprovedHttpOriginsOptions> originsOptions, IOptions<SafeHttpOptions> httpOptions)
        {
            var global = httpOptions.Value;
            var policies = new Dictionary<string, OutboundRequestPolicy>(StringComparer.Ordinal);

            foreach (var pair in originsOptions.Value.Origins)
            {
                var originId = pair.Key.Trim().ToLowerInvariant();
                var configured = pair.Value;

                var policy = new OutboundRequestPolicy(
                    originId,
                    new Uri(configured.BaseUri, UriKind.Absolute),
                    configured.Enabled,
                    configured.AllowedMethods
                        .Select(method => method.Trim().ToUpperInvariant())
                        .ToHashSet(StringComparer.Ordinal),
                    configured.AllowedPathPrefixes
                        .Select(SafeHttpUriBuilder.NormalizeConfiguredPathPrefix)
                        .ToArray(),
                    configured.AllowedQueryParameters
                        .Select(name => name.Trim())
                        .ToHashSet(StringComparer.Ordinal),
                    configured.SelectedResponseHeaders
                        .Select(name => name.Trim().ToLowerInvariant())
                        .ToHashSet(StringComparer.OrdinalIgnoreCase),
                    configured.MaximumResponseBytes ?? global.MaxDecompressedResponseBytes,
                    configured.ResponseHeadersTimeoutMilliseconds ?? global.ResponseHeadersTimeoutMilliseconds,
                    configured.BodyReadTimeoutMilliseconds ?? global.BodyReadTimeoutMilliseconds);

                if (!policies.TryAdd(originId, policy))
                {
                    throw new InvalidOperationException($"Duplicate approved HTTP origin '{originId}'.");
                }
            }

            _policies = policies;
        }

        public IReadOnlyCollection<string> OriginIds => _policies.Keys.ToArray();

        public bool TryGet(string originId, out OutboundRequestPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(originId))
            {
                policy = default!;
                return false;
            }

            return _policies.TryGetValue(originId.Trim().ToLowerInvariant(), out policy!);
        }

        public OutboundRequestPolicy GetRequired(string originId) =>
            TryGet(originId, out var policy) ? policy : throw new KeyNotFoundException($"No approved HTTP origin is registered for '{originId}'.");
    }
}
