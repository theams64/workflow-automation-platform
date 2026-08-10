using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Api.WorkflowEngine.References
{
    public sealed partial class WorkflowReferenceParser : IWorkflowReferenceParser
    {
        [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.CultureInvariant)]
        private static partial Regex TemplateRegex();

        public bool TryParse(string expression, out WorkflowReference reference)
        {
            reference = default!;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var value = expression.Trim();

            if (value.StartsWith("workflow.inputs.", StringComparison.Ordinal))
            {
                reference = new(WorkflowReferenceScope.WorkflowInputs, null, value["workflow.inputs.".Length..]);
                return HasPath(reference);
            }

            if (value.StartsWith("execution.", StringComparison.Ordinal))
            {
                reference = new(WorkflowReferenceScope.Execution, null, value["execution.".Length..]);
                return HasPath(reference);
            }

            if (value.StartsWith("item.", StringComparison.Ordinal))
            {
                reference = new(WorkflowReferenceScope.CurrentItem, null, value["item.".Length..]);
                return HasPath(reference);
            }

            if (value.StartsWith("steps.", StringComparison.Ordinal))
            {
                var remainder = value["steps.".Length..];
                var outputMarker = remainder.IndexOf(".output.", StringComparison.Ordinal);

                if (outputMarker <= 0)
                {
                    return false;
                }

                var stepKey = remainder[..outputMarker];
                var path = remainder[(outputMarker + ".output.".Length)..];

                reference = new(WorkflowReferenceScope.StepOutput, stepKey, path);
                return HasPath(reference);
            }

            return false;
        }

        public IReadOnlyList<WorkflowReference> FindReferences(string configJson)
        {
            using var document = JsonDocument.Parse(configJson);
            var results = new List<WorkflowReference>();
            Visit(document.RootElement, results);
            return results;
        }

        private void Visit(JsonElement element, List<WorkflowReference> results)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Visit(property.Value, results);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        Visit(item, results);
                    }
                    break;

                case JsonValueKind.String:
                    var text = element.GetString();
                    if (text is null)
                    {
                        return;
                    }

                    foreach (Match match in TemplateRegex().Matches(text))
                    {
                        if (TryParse(match.Groups[1].Value, out var reference))
                        {
                            results.Add(reference);
                        }
                    }
                    break;
            }
        }

        private static bool HasPath(WorkflowReference reference) => 
            !string.IsNullOrWhiteSpace(reference.Path) && reference.Path.Split('.').All(IsSafeSegment);

        private static bool IsSafeSegment(string segment) => 
            segment.Length > 0 && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
    }
}
