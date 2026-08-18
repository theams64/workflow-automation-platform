using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;

namespace Backend.Api.Configuration
{
    public sealed partial class ApprovedHttpOriginsOptionsValidator(IOptions<SafeHttpOptions> httpOptions) : IValidateOptions<ApprovedHttpOriginsOptions>
    {
        [GeneratedRegex(@"^[a-z][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant)]
        private static partial Regex OriginIdPattern();

        [GeneratedRegex(@"^[A-Za-z0-9._~-]+$", RegexOptions.CultureInvariant)]
        private static partial Regex QueryNamePattern();

        public ValidateOptionsResult Validate(string? name, ApprovedHttpOriginsOptions options)
        {
            var errors = new List<string>();
            var safeHttp = httpOptions.Value;

            if (options.Origins is null)
            {
                return ValidateOptionsResult.Fail("ApprovedHttpOrigins:Origins is required.");
            }

            if (options.Origins.Count > WorkflowLimits.HttpMaximumApprovedOrigins)
            {
                errors.Add($"No more than {WorkflowLimits.HttpMaximumApprovedOrigins} approved HTTP origins may be configured.");
            }

            foreach (var pair in options.Origins)
            {
                if (pair.Value is null)
                {
                    errors.Add($"Approved HTTP origin '{pair.Key}' configuration is required.");
                    continue;
                }

                ValidateOrigin(pair.Key, pair.Value, safeHttp, errors);
            }

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }

        private static void ValidateOrigin(string originId, ApprovedHttpOriginOptions origin, SafeHttpOptions safeHttp, ICollection<string> errors)
        {
            var normalizedId = originId.Trim().ToLowerInvariant();

            if (!OriginIdPattern().IsMatch(normalizedId))
            {
                errors.Add($"Approved HTTP origin id '{originId}' is invalid.");
            }

            if (!Uri.TryCreate(origin.BaseUri, UriKind.Absolute, out var baseUri) ||
                !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(baseUri.Host) ||
                !string.IsNullOrEmpty(baseUri.UserInfo) ||
                !string.IsNullOrEmpty(baseUri.Query) ||
                !string.IsNullOrEmpty(baseUri.Fragment) ||
                baseUri.AbsolutePath != "/")
            {
                errors.Add($"Approved HTTP origin '{originId}' must use an HTTPS origin-only BaseUri.");
            }
            else if (IPAddress.TryParse(baseUri.Host.Trim('[', ']'), out var literalAddress) && !PublicNetworkAddressPolicy.IsAllowed(literalAddress))
            {
                errors.Add($"Approved HTTP origin '{originId}' cannot target a non-public IP address.");
            }

            if (origin.AllowedMethods is null || 
                origin.AllowedMethods.Count == 0 || 
                origin.AllowedMethods.Any(method => string.IsNullOrWhiteSpace(method) ||
                !string.Equals(method.Trim(), "GET", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"Approved HTTP origin '{originId}' may allow only GET in Level 1.");
            }

            if (origin.AllowedPathPrefixes is null ||
                origin.AllowedPathPrefixes.Count == 0 ||
                origin.AllowedPathPrefixes.Count > WorkflowLimits.HttpMaximumPathPrefixes ||
                origin.AllowedPathPrefixes.Any(prefix => !SafeHttpUriBuilder.IsSafeConfiguredPathPrefix(prefix)))
            {
                errors.Add($"Approved HTTP origin '{originId}' has invalid allowed path prefixes.");
            }

            if (origin.AllowedQueryParameters is null ||
                origin.AllowedQueryParameters.Count > WorkflowLimits.HttpMaximumQueryParameters ||
                origin.AllowedQueryParameters.Any(query => string.IsNullOrWhiteSpace(query) ||
                query.Length > safeHttp.MaxQueryNameLength || 
                !QueryNamePattern().IsMatch(query)))
            {
                errors.Add($"Approved HTTP origin '{originId}' has invalid query-parameter names.");
            }

            if (origin.AllowedQueryParameters is not null &&
                origin.AllowedQueryParameters
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .Any(group => group.Count() > 1))
            {
                errors.Add($"Approved HTTP origin '{originId}' contains duplicate query-parameter names.");
            }

            if (origin.SelectedResponseHeaders is null ||
                origin.SelectedResponseHeaders.Count > WorkflowLimits.HttpMaximumSelectedResponseHeaders ||
                origin.SelectedResponseHeaders.Any(header => !SafeHttpHeaderPolicy.IsSelectableResponseHeader(header)))
            {
                errors.Add($"Approved HTTP origin '{originId}' contains a response header that cannot be selected.");
            }

            if (origin.MaximumResponseBytes is int responseBytes && (responseBytes <= 0 || responseBytes > safeHttp.MaxDecompressedResponseBytes))
            {
                errors.Add($"Approved HTTP origin '{originId}' has an invalid maximum response size.");
            }

            if (origin.ResponseHeadersTimeoutMilliseconds is int headerTimeout && (headerTimeout <= 0 || headerTimeout > safeHttp.ResponseHeadersTimeoutMilliseconds))
            {
                errors.Add($"Approved HTTP origin '{originId}' has an invalid response-header timeout.");
            }

            if (origin.BodyReadTimeoutMilliseconds is int bodyTimeout && (bodyTimeout <= 0 || bodyTimeout > safeHttp.BodyReadTimeoutMilliseconds))
            {
                errors.Add($"Approved HTTP origin '{originId}' has an invalid body-read timeout.");
            }
        }
    }
}
