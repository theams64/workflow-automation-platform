using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Validation;
using System.Text.RegularExpressions;

namespace Backend.Api.WorkflowEngine.Validation
{
    public static partial class StepKeyValidator
    {
        [GeneratedRegex(@"^[a-z][a-z0-9_]{0,63}$", RegexOptions.CultureInvariant)]
        private static partial Regex Pattern();

        public static bool IsValid(string? value) =>
            value is not null && value.Length <= WorkflowLimits.StepKeyMaxLength && Pattern().IsMatch(value);
    }
}
