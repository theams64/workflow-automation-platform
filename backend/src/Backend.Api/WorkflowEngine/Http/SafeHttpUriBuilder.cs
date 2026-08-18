using Backend.Api.Configuration;
using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Execution;
using System.Text;
using System.Text.RegularExpressions;

namespace Backend.Api.WorkflowEngine.Http
{
    public static partial class SafeHttpUriBuilder
    {
        [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z", RegexOptions.CultureInvariant)]
        private static partial Regex SafeSegmentPattern();

        [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z", RegexOptions.CultureInvariant)]
        private static partial Regex SafeQueryNamePattern();

        public static Uri Build(SafeHttpRequest request, OutboundRequestPolicy policy, SafeHttpOptions options)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(policy);
            ArgumentNullException.ThrowIfNull(options);

            if (!policy.Enabled)
            {
                throw new SafeHttpException(ExecutionErrorCodes.OriginDisabled, "The selected HTTP origin is disabled.");
            }

            if (!string.Equals(request.OriginId, policy.OriginId, StringComparison.OrdinalIgnoreCase))
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidOrigin, "The selected HTTP origin is invalid.");
            }

            var method = request.Method.Method.ToUpperInvariant();
            if (method != HttpMethod.Get.Method || !policy.AllowedMethods.Contains(method))
            {
                throw new SafeHttpException(ExecutionErrorCodes.MethodNotAllowed, "The HTTP method is not allowed for this origin.");
            }

            if (request.MaximumResponseBytes <= 0 || request.MaximumResponseBytes > policy.MaximumResponseBytes || request.MaximumResponseBytes > options.MaxDecompressedResponseBytes)
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidStepConfiguration, "The requested response-size limit is invalid.");
            }

            if (!IsSafeMaterializedPath(request.Path) || Encoding.UTF8.GetByteCount(request.Path) > WorkflowLimits.HttpPathMaxLength)
            {
                throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "The HTTP request path is invalid.");
            }

            if (!policy.AllowedPathPrefixes.Any(prefix => IsPathAllowed(request.Path, prefix)))
            {
                throw new SafeHttpException(ExecutionErrorCodes.PathNotAllowed, "The HTTP request path is not allowed for this origin.");
            }

            if (request.Query.Count > options.MaxQueryParameters)
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidStepConfiguration, "The HTTP request contains too many query parameters.");
            }

            foreach (var pair in request.Query)
            {
                if (!IsSafeQueryName(pair.Key, options.MaxQueryNameLength))
                {
                    throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "An HTTP query-parameter name is invalid.");
                }

                if (!policy.AllowedQueryParameters.Contains(pair.Key))
                {
                    throw new SafeHttpException(ExecutionErrorCodes.QueryParameterNotAllowed, "An HTTP query parameter is not allowed for this origin.");
                }

                var value = pair.Value ?? string.Empty;
                if (Encoding.UTF8.GetByteCount(value) > options.MaxQueryValueLength || ContainsControlCharacter(value))
                {
                    throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "An HTTP query-parameter value is invalid.");
                }
            }

            var authority = policy.BaseUri.GetLeftPart(UriPartial.Authority);
            var query = BuildQuery(request.Query);
            var text = string.Concat(authority, request.Path, query);

            if (Encoding.UTF8.GetByteCount(text) > options.MaxUrlLength || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "The HTTP request URI is invalid.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || 
                !string.Equals(uri.IdnHost, policy.BaseUri.IdnHost, StringComparison.OrdinalIgnoreCase) || 
                uri.Port != policy.BaseUri.Port || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "The HTTP request URI is invalid.");
            }

            return uri;
        }

        public static bool IsSafeConfiguredPathPrefix(string value)
        {
            try
            {
                _ = NormalizeConfiguredPathPrefix(value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string NormalizeConfiguredPathPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("An allowed HTTP path prefix cannot be empty.", nameof(value));
            }

            var normalized = value.Trim();

            if (!IsSafeMaterializedPath(normalized))
            {
                throw new ArgumentException("An allowed HTTP path prefix is invalid.", nameof(value));
            }

            return normalized.Length == 1 ? normalized : normalized.TrimEnd('/');
        }

        public static bool IsSafePathSegment(string value)
        {
            if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) > WorkflowLimits.HttpPathSegmentMaxLength)
            {
                return false;
            }

            return value is not "." and not ".." && SafeSegmentPattern().IsMatch(value);
        }

        public static bool IsSafeMaterializedPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith("/", StringComparison.Ordinal) ||
                value.StartsWith("//", StringComparison.Ordinal) ||
                value.Contains('%') ||
                value.Contains('\\') ||
                value.Contains('?') ||
                value.Contains('#') ||
                ContainsControlCharacter(value))
            {
                return false;
            }

            if (value == "/")
            {
                return true;
            }

            var body = value[1..];
            if (body.EndsWith("/", StringComparison.Ordinal))
            {
                body = body[..^1];
            }

            if (body.Length == 0)
            {
                return true;
            }

            var segments = body.Split('/', StringSplitOptions.None);
            return segments.All(IsSafePathSegment);
        }

        public static bool IsPathAllowed(string path, string configuredPrefix)
        {
            var prefix = NormalizeConfiguredPathPrefix(configuredPrefix);

            if (prefix == "/")
            {
                return true;
            }

            return string.Equals(path, prefix, StringComparison.Ordinal) || (path.StartsWith(prefix, StringComparison.Ordinal) && path.Length > prefix.Length && path[prefix.Length] == '/');
        }

        private static bool IsSafeQueryName(string value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength && SafeQueryNamePattern().IsMatch(value);

        private static string BuildQuery(IReadOnlyDictionary<string, string?> query)
        {
            if (query.Count == 0)
            {
                return string.Empty;
            }

            var parts = query
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? string.Empty)}");

            return "?" + string.Join("&", parts);
        }

        private static bool ContainsControlCharacter(string value) => value.Any(char.IsControl);
    }
}
