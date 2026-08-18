using Backend.Api.Configuration;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.References;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Http.Level1
{
    public sealed class Level1HttpRequestMaterializer(IWorkflowReferenceParser referenceParser, IWorkflowReferenceResolver referenceResolver, IOptions<SafeHttpOptions> options)
    {
        private readonly SafeHttpOptions _options = options.Value;

        public IReadOnlyList<ServiceError> ValidateConfiguration(Level1HttpStepConfiguration configuration, OutboundRequestPolicy policy, int stepNumber)
        {
            var errors = new List<ServiceError>();

            if (string.IsNullOrWhiteSpace(configuration.Path))
            {
                errors.Add(new("workflow_step.http_path_required", $"Step {stepNumber}: an HTTP path is required."));
            }
            else
            {
                ValidatePathTemplate(configuration.Path, stepNumber, errors);

                var validationPath = BuildPolicyValidationPath(configuration.Path);

                if (validationPath is not null && !policy.AllowedPathPrefixes.Any(prefix => SafeHttpUriBuilder.IsPathAllowed(validationPath, prefix)))
                {
                    errors.Add(new("workflow_step.http_path_not_allowed", $"Step {stepNumber}: the HTTP path is not allowed for the selected origin."));
                }
            }

            if (configuration.Query is null)
            {
                errors.Add(new("workflow_step.http_query_invalid", $"Step {stepNumber}: the HTTP query object is invalid."));
            }
            else
            {
                if (configuration.Query.Count > _options.MaxQueryParameters)
                {
                    errors.Add(new("workflow_step.http_query_too_many", $"Step {stepNumber}: the HTTP query contains too many parameters."));
                }

                foreach (var pair in configuration.Query)
                {
                    if (!policy.AllowedQueryParameters.Contains(pair.Key))
                    {
                        errors.Add(new("workflow_step.http_query_not_allowed", $"Step {stepNumber}: query parameter '{pair.Key}' is not allowed for the selected origin."));
                        continue;
                    }

                    if (pair.Key.Length > _options.MaxQueryNameLength)
                    {
                        errors.Add(new("workflow_step.http_query_invalid", $"Step {stepNumber}: an HTTP query-parameter name is too long."));
                    }

                    ValidateTemplateOrLiteral(pair.Value, stepNumber, "query value", errors);
                }
            }

            if (configuration.MaximumResponseBytes is int requested && (requested <= 0 || requested > policy.MaximumResponseBytes || requested > _options.MaxDecompressedResponseBytes))
            {
                errors.Add(new("workflow_step.http_response_limit_invalid", $"Step {stepNumber}: the requested HTTP response-size limit is invalid."));
            }

            return errors;
        }

        public SafeHttpRequest Materialize(Level1HttpStepConfiguration configuration, OutboundRequestPolicy policy, StepExecutionContext context)
        {
            var path = MaterializePath(configuration.Path, context.WorkflowContext);

            var query = new Dictionary<string, string?>(StringComparer.Ordinal);

            foreach (var pair in configuration.Query)
            {
                query[pair.Key] = MaterializeTemplateOrLiteral(pair.Value, context.WorkflowContext);
            }

            var maximumResponseBytes = configuration.MaximumResponseBytes ?? policy.MaximumResponseBytes;

            return new SafeHttpRequest(context.Workflow.UserID, context.Workflow.ID, policy.OriginId, HttpMethod.Get, path, query, maximumResponseBytes);
        }

        private void ValidatePathTemplate(string path, int stepNumber, ICollection<ServiceError> errors)
        {
            if (Encoding.UTF8.GetByteCount(path) > WorkflowLimits.HttpPathMaxLength ||
                !path.StartsWith("/", StringComparison.Ordinal) ||
                path.StartsWith("//", StringComparison.Ordinal) ||
                path.Contains('%') ||
                path.Contains('\\') ||
                path.Contains('?') ||
                path.Contains('#') ||
                path.Any(char.IsControl))
            {
                errors.Add(new("workflow_step.http_path_invalid", $"Step {stepNumber}: the HTTP path is invalid."));
                return;
            }

            if (path == "/")
            {
                return;
            }

            var body = path[1..];
            if (body.EndsWith("/", StringComparison.Ordinal))
            {
                body = body[..^1];
            }

            foreach (var segment in body.Split('/', StringSplitOptions.None))
            {
                if (TryParseCompleteReference(segment, out var reference))
                {
                    ValidateAllowedReference(reference, stepNumber, errors); 
                    continue;
                }

                if (ContainsTemplateMarker(segment) || !SafeHttpUriBuilder.IsSafePathSegment(segment))
                {
                    errors.Add(new("workflow_step.http_path_invalid", $"Step {stepNumber}: each HTTP path reference must occupy a complete safe path segment."));
                }
            }
        }

        private string? BuildPolicyValidationPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("/", StringComparison.Ordinal) ||
                path.StartsWith("//", StringComparison.Ordinal) ||
                path.Contains('%') ||
                path.Contains('\\') ||
                path.Contains('?') ||
                path.Contains('#') ||
                path.Any(char.IsControl))
            {
                return null;
            }

            if (path == "/")
            {
                return path;
            }

            var hasTrailingSlash = path.EndsWith("/", StringComparison.Ordinal);
            var body = path[1..];

            if (hasTrailingSlash)
            {
                body = body[..^1];
            }

            var segments = body.Split('/', StringSplitOptions.None);

            for (var index = 0; index < segments.Length; index++)
            {
                if (TryParseCompleteReference(segments[index], out _))
                {
                    segments[index] = "reference";
                    continue;
                }

                if (ContainsTemplateMarker(segments[index]) || !SafeHttpUriBuilder.IsSafePathSegment(segments[index]))
                {
                    return null;
                }
            }

            var result = "/" + string.Join('/', segments);
            return hasTrailingSlash ? result + "/" : result;
        }

        private void ValidateTemplateOrLiteral(string? value, int stepNumber, string fieldName, ICollection<ServiceError> errors)
        {
            if (value is null)
            {
                return;
            }

            if (Encoding.UTF8.GetByteCount(value) > _options.MaxQueryValueLength || value.Any(char.IsControl))
            {
                errors.Add(new("workflow_step.http_query_invalid", $"Step {stepNumber}: an HTTP {fieldName} is invalid."));
                return;
            }

            if (TryParseCompleteReference(value, out var reference))
            {
                ValidateAllowedReference(reference, stepNumber, errors);
                return;
            }

            if (ContainsTemplateMarker(value))
            {
                errors.Add(new("workflow_step.http_template_invalid", $"Step {stepNumber}: HTTP references must occupy the complete configured value."));
            }
        }

        private static void ValidateAllowedReference(WorkflowReference reference, int stepNumber, ICollection<ServiceError> errors)
        {
            if (reference.Scope == WorkflowReferenceScope.CurrentItem)
            {
                errors.Add(new("workflow_step.reference_invalid", $"Step {stepNumber}: item references are not supported by Level 1 HTTP."));
                return;
            }

            if (reference.Scope == WorkflowReferenceScope.Execution && !WorkflowReferencePaths.IsSupportedExecutionPath(reference.Path))
            {
                errors.Add(new("workflow_step.reference_unknown_field", $"Step {stepNumber}: a reference targets an unknown execution field."));
            }
        }

        private string MaterializePath(string path, WorkflowExecutionContext context)
        {
            if (path == "/")
            {
                return path;
            }

            var hasTrailingSlash = path.EndsWith("/", StringComparison.Ordinal);
            var body = path[1..];

            if (hasTrailingSlash)
            {
                body = body[..^1];
            }

            var segments = body.Split('/', StringSplitOptions.None);

            for (var index = 0; index < segments.Length; index++)
            {
                if (!TryParseCompleteReference(segments[index], out var reference))
                {
                    continue;
                }

                var resolved = ResolveScalar(reference, context);

                if (!SafeHttpUriBuilder.IsSafePathSegment(resolved))
                {
                    throw new SafeHttpException(ExecutionErrorCodes.RequestUriInvalid, "An HTTP path reference resolved to an invalid path segment.");
                }

                segments[index] = resolved;
            }

            var result = "/" + string.Join('/', segments);
            return hasTrailingSlash ? result + "/" : result;
        }

        private string? MaterializeTemplateOrLiteral(string? value, WorkflowExecutionContext context)
        {
            if (value is null)
            {
                return null;
            }

            return TryParseCompleteReference(value, out var reference) ? ResolveScalar(reference, context) : value;
        }

        private string ResolveScalar(WorkflowReference reference, WorkflowExecutionContext context)
        {
            if (!referenceResolver.TryResolve(reference, context, out var value))
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidReference, "An HTTP configuration reference could not be resolved.");
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw new SafeHttpException(ExecutionErrorCodes.InvalidReference, "An HTTP configuration reference must resolve to a scalar value.")
            };
        }

        private bool TryParseCompleteReference(string value, out WorkflowReference reference)
        {
            reference = default!;
            var trimmed = value.Trim();

            if (trimmed.Length < 5 || !trimmed.StartsWith("{{", StringComparison.Ordinal) || !trimmed.EndsWith("}}", StringComparison.Ordinal))
            {
                return false;
            }

            var expression = trimmed[2..^2].Trim();

            if (expression.Contains('{') || expression.Contains('}'))
            {
                return false;
            }

            return referenceParser.TryParse(expression, out reference);
        }

        private static bool ContainsTemplateMarker(string value) => value.Contains("{{", StringComparison.Ordinal) || value.Contains("}}", StringComparison.Ordinal);
    }
}
