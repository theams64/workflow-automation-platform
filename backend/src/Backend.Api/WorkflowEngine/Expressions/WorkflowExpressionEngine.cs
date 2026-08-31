using Backend.Api.Configuration;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Backend.Api.WorkflowEngine.Expressions
{
    public sealed partial class WorkflowExpressionEngine
    {
        private readonly IWorkflowReferenceParser _referenceParser;
        private readonly IWorkflowReferenceResolver _referenceResolver;
        private readonly ITimezoneValidator _timezoneValidator;
        private readonly WorkflowExpressionOptions _options;

        private static readonly HashSet<string> AllowedFunctions = new(StringComparer.Ordinal)
        {
            "default",
            "toString",
            "toNumber",
            "toBoolean",
            "toDateTime",
            "round",
            "formatCurrency",
            "formatDateTime",
            "truncate",
            "uppercase",
            "lowercase",
            "join",
            "count",
            "priceText"
        };

        public WorkflowExpressionEngine(IWorkflowReferenceParser referenceParser, IWorkflowReferenceResolver referenceResolver, ITimezoneValidator timezoneValidator, IOptions<WorkflowExpressionOptions> options)
        {
            _referenceParser = referenceParser;
            _referenceResolver = referenceResolver;
            _timezoneValidator = timezoneValidator;
            _options = options.Value;
        }

        public IReadOnlyList<ServiceError> ValidateTemplate(string template, WorkflowValidationContext context, bool allowItemReferences, string errorCode = "workflow_step.expression_invalid")
        {
            var errors = new List<ServiceError>();

            if (Encoding.UTF8.GetByteCount(template ?? string.Empty) > _options.MaxTemplateBytes)
            {
                errors.Add(new(errorCode, $"Step {context.CurrentStepOrder}: an expression template exceeded the allowed size."));
                return errors;
            }

            try
            {
                foreach (var part in ParseTemplate(template ?? string.Empty))
                {
                    if (part.Expression is null)
                    {
                        continue;
                    }

                    ValidateNode(part.Expression, context, allowItemReferences, errors, errorCode);
                }
            }
            catch (WorkflowExpressionException)
            {
                errors.Add(new(errorCode, $"Step {context.CurrentStepOrder}: an expression template is invalid."));
            }

            return errors;
        }

        public bool TryParseCompleteReferenceTemplate(string template, out WorkflowReference reference)
        {
            reference = default!;

            try
            {
                var parts = ParseTemplate(template);
                if (parts.Count != 1 || parts[0].Expression is not ReferenceNode referenceNode || parts[0].Literal is not null)
                {
                    return false;
                }

                reference = referenceNode.Reference;
                return true;
            }
            catch (WorkflowExpressionException)
            {
                return false;
            }
        }

        public JsonElement EvaluateTemplate(string template, ExpressionEvaluationContext context, ExpressionBudget budget)
        {
            var parts = ParseTemplate(template);
            budget.Consume();

            if (parts.Count == 1 && parts[0].Expression is not null && parts[0].Literal is null)
            {
                var value = EvaluateNode(parts[0].Expression, context, budget);

                return RequirePresent(value).Clone();
            }

            var builder = new StringBuilder();

            foreach (var part in parts)
            {
                budget.Consume();

                if (part.Literal is not null)
                {
                    builder.Append(part.Literal);
                    continue;
                }

                var value = RequirePresent(EvaluateNode(part.Expression!, context, budget));

                builder.Append(ScalarToString(value));

                EnsureStringLimit(builder.ToString());
            }

            var rendered = builder.ToString();
            EnsureStringLimit(rendered);
            return JsonSerializer.SerializeToElement(rendered);
        }

        public string RenderTextTemplate(string template, ExpressionEvaluationContext context, ExpressionBudget budget)
        {
            var parts = ParseTemplate(template);
            var builder = new StringBuilder();

            foreach (var part in parts)
            {
                budget.Consume();

                if (part.Literal is not null)
                {
                    builder.Append(part.Literal);
                }
                else
                {
                    var value = RequirePresent(EvaluateNode(part.Expression!, context, budget));

                    builder.Append(ScalarToString(value));
                }

                EnsureStringLimit(builder.ToString());
            }

            return builder.ToString();
        }

        private void ValidateNode(ExpressionNode node, WorkflowValidationContext context, bool allowItemReferences, ICollection<ServiceError> errors, string errorCode)
        {
            switch (node)
            {
                case LiteralNode:
                    return;

                case ReferenceNode referenceNode:
                    ValidateReference(referenceNode.Reference, context, allowItemReferences, errors, errorCode);
                    return;

                case FunctionNode function:
                    if (!AllowedFunctions.Contains(function.Name) || function.Arguments.Count > _options.MaxFunctionArguments)
                    {
                        errors.Add(new(errorCode, $"Step {context.CurrentStepOrder}: an expression contains an unsupported function."));
                        return;
                    }

                    if (!IsValidArity(function.Name, function.Arguments.Count))
                    {
                        errors.Add(new(errorCode, $"Step {context.CurrentStepOrder}: an expression function has an invalid argument count."));
                        return;
                    }

                    foreach (var argument in function.Arguments)
                    {
                        ValidateNode(argument, context, allowItemReferences, errors, errorCode);
                    }
                    return;
            }
        }

        private static void ValidateReference(WorkflowReference reference, WorkflowValidationContext context, bool allowItemReferences, ICollection<ServiceError> errors, string errorCode)
        {
            if (reference.Scope == WorkflowReferenceScope.CurrentItem)
            {
                if (!allowItemReferences)
                {
                    errors.Add(new(errorCode, $"Step {context.CurrentStepOrder}: item references are valid only inside engine-controlled collection iteration."));
                }

                return;
            }

            if (reference.Scope == WorkflowReferenceScope.Execution)
            {
                if (!WorkflowReferencePaths.IsSupportedExecutionPath(reference.Path))
                {
                    errors.Add(new("workflow_step.reference_unknown_field", $"Step {context.CurrentStepOrder}: a reference targets an unknown execution field."));
                }

                return;
            }

            if (reference.Scope != WorkflowReferenceScope.StepOutput)
            {
                return;
            }

            if (reference.StepKey is null || !context.PriorStepSchemas.TryGetValue(reference.StepKey, out var schema))
            {
                errors.Add(new("workflow_step.reference_invalid", $"Step {context.CurrentStepOrder}: a reference targets an unknown or later step."));
                return;
            }

            if (!schema.Supports(reference.Path))
            {
                errors.Add(new("workflow_step.reference_unknown_field", $"Step {context.CurrentStepOrder}: a reference targets an unknown output field."));
            }
        }

        private EvaluatedValue EvaluateNode(ExpressionNode node, ExpressionEvaluationContext context, ExpressionBudget budget)
        {
            budget.Consume();

            return node switch
            {
                LiteralNode literal => new(false, literal.Value),
                ReferenceNode reference => ResolveReference(reference.Reference, context),
                FunctionNode function => EvaluateFunction(function, context, budget),
                _ => throw InvalidExpression()
            };
        }

        private EvaluatedValue ResolveReference(WorkflowReference reference, ExpressionEvaluationContext context)
        {
            if (reference.Scope == WorkflowReferenceScope.CurrentItem)
            {
                if (context.CurrentItem is not JsonElement item)
                {
                    return EvaluatedValue.Missing;
                }

                return TryResolvePath(item, reference.Path, out var itemValue) ? new(false, itemValue) : EvaluatedValue.Missing;
            }

            return _referenceResolver.TryResolve(reference, context.WorkflowContext, out var value) ? new(false, value) : EvaluatedValue.Missing;
        }

        private EvaluatedValue EvaluateFunction(FunctionNode function, ExpressionEvaluationContext context, ExpressionBudget budget)
        {
            if (!AllowedFunctions.Contains(function.Name) || !IsValidArity(function.Name, function.Arguments.Count))
            {
                throw InvalidExpression();
            }

            var arguments = function.Arguments
                .Select(argument => EvaluateNode(argument, context, budget))
                .ToArray();

            return function.Name switch
            {
                "default" => Default(arguments[0], arguments[1]),
                "toString" => FromString(ToRequiredString(arguments[0])),
                "toNumber" => FromDecimal(ToDecimal(arguments[0])),
                "toBoolean" => FromBoolean(ToBoolean(arguments[0])),
                "toDateTime" => FromString(ToDateTime(arguments[0]).ToString("O", CultureInfo.InvariantCulture)),
                "round" => FromDecimal(Round(arguments[0], arguments[1])),
                "formatCurrency" => FromString(FormatCurrency(arguments[0], arguments[1])),
                "formatDateTime" => FromString(FormatDateTime(arguments[0], arguments[1], arguments[2])),
                "truncate" => FromString(Truncate(arguments[0], arguments[1])),
                "uppercase" => FromString(ToRequiredString(arguments[0]).ToUpperInvariant()),
                "lowercase" => FromString(ToRequiredString(arguments[0]).ToLowerInvariant()),
                "join" => FromString(Join(arguments[0], arguments[1])),
                "count" => FromInt32(Count(arguments[0])),
                "priceText" => FromString($"From {FormatCurrency(arguments[0], arguments[1])}"),
                _ => throw InvalidExpression()
            };
        }

        private EvaluatedValue Default(EvaluatedValue value, EvaluatedValue fallback)
        {
            var useFallback = 
                value.IsMissing || 
                value.Value.ValueKind == JsonValueKind.Null || 
                (value.Value.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(value.Value.GetString()));

            if (!useFallback)
            {
                return value;
            }

            return fallback.IsMissing ? throw InvalidReference() : fallback;
        }

        private string FormatCurrency(EvaluatedValue numberValue, EvaluatedValue currencyValue)
        {
            var amount = ToDecimal(numberValue);
            var currency = ToRequiredString(currencyValue).Trim().ToUpperInvariant();

            var formatted = currency switch
            {
                "USD" => $"${amount:0.00}",
                "EUR" => $"€{amount:0.00}",
                "GBP" => $"£{amount:0.00}",
                "JPY" => $"¥{amount:0}",
                "CAD" => $"CA${amount:0.00}",
                "AUD" => $"A${amount:0.00}",
                _ when currency.Length is >= 3 and <= 8 && currency.All(character => char.IsAsciiLetter(character)) => $"{currency} {amount:0.00}",
                _ => throw new WorkflowExpressionException("expression_conversion_failed", "A currency value was invalid.") 
            };

            EnsureStringLimit(formatted);
            return formatted;
        }

        private string FormatDateTime(EvaluatedValue dateValue, EvaluatedValue formatValue, EvaluatedValue timezoneValue)
        {
            var date = ToDateTime(dateValue);
            var format = ToRequiredString(formatValue);
            var timezone = ToRequiredString(timezoneValue);

            if (format.Length == 0 || format.Length > 128 || format.Any(char.IsControl))
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "A date-time format was invalid.");
            }

            TimeZoneInfo zone;
            try
            {
                zone = _timezoneValidator.GetRequired(timezone);
            }
            catch (ArgumentException)
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "A date-time timezone was invalid.");
            }

            var local = TimeZoneInfo.ConvertTime(date, zone);
            var result = local.ToString(format, CultureInfo.InvariantCulture);
            EnsureStringLimit(result);
            return result;
        }

        private string Truncate(EvaluatedValue textValue, EvaluatedValue lengthValue)
        {
            var text = ToRequiredString(textValue);
            var maximumLength = ToInt32(lengthValue);

            if (maximumLength < 0 || maximumLength > 100_000)
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "A truncate length was invalid.");
            }

            var result = text.Length <= maximumLength ? text : text[..maximumLength];

            EnsureStringLimit(result);
            return result;
        }

        private string Join(EvaluatedValue collectionValue, EvaluatedValue separatorValue)
        {
            var collection = RequirePresent(collectionValue);
            var separator = ToRequiredString(separatorValue);

            if (collection.ValueKind != JsonValueKind.Array)
            {
                throw WrongType("The join function requires an array.");
            }

            var parts = new List<string>();

            foreach (var item in collection.EnumerateArray())
            {
                parts.Add(ScalarToString(item));

                if (parts.Count > 1_000)
                {
                    throw new WorkflowExpressionException("expression_collection_limit", "An expression collection exceeded the allowed size.");
                }
            }

            var result = string.Join(separator, parts);
            EnsureStringLimit(result);
            return result;
        }

        private static int Count(EvaluatedValue value)
        {
            var present = RequirePresent(value);

            return present.ValueKind switch
            {
                JsonValueKind.Array => present.GetArrayLength(),
                JsonValueKind.Object => present.EnumerateObject().Count(),
                _ => throw WrongType("The count function requires an array or object.")
            };
        }

        private static decimal Round(EvaluatedValue numberValue, EvaluatedValue decimalsValue)
        {
            var number = ToDecimal(numberValue);
            var decimals = ToInt32(decimalsValue);

            if (decimals < 0 || decimals > 6)
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "The round precision was invalid.");
            }

            return Math.Round(number, decimals, MidpointRounding.AwayFromZero);
        }

        private static DateTimeOffset ToDateTime(EvaluatedValue value)
        {
            var text = ToRequiredString(value);

            if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "A value could not be converted to a date-time.");
            }

            return result;
        }

        private static decimal ToDecimal(EvaluatedValue value)
        {
            var present = RequirePresent(value);

            if (present.ValueKind == JsonValueKind.Number && present.TryGetDecimal(out var number))
            {
                return number;
            }

            if (present.ValueKind == JsonValueKind.String && decimal.TryParse(present.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            throw new WorkflowExpressionException("expression_conversion_failed", "A value could not be converted to a number.");
        }

        private static bool ToBoolean(EvaluatedValue value)
        {
            var present = RequirePresent(value);

            if (present.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return present.GetBoolean();
            }

            if (present.ValueKind == JsonValueKind.String && bool.TryParse(present.GetString(), out var boolean))
            {
                return boolean;
            }

            throw new WorkflowExpressionException("expression_conversion_failed", "A value could not be converted to a boolean.");
        }

        private static int ToInt32(EvaluatedValue value)
        {
            var number = ToDecimal(value);

            if (number != decimal.Truncate(number) || number < int.MinValue || number > int.MaxValue)
            {
                throw new WorkflowExpressionException("expression_conversion_failed", "A value could not be converted to an integer.");
            }

            return (int)number;
        }

        private static string ToRequiredString(EvaluatedValue value)
        {
            var present = RequirePresent(value);

            return present.ValueKind switch
            {
                JsonValueKind.String => present.GetString() ?? string.Empty,
                JsonValueKind.Number => present.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => throw WrongType("A scalar string-compatible value was required.")
            };
        }

        private static string ScalarToString(JsonElement value) =>
            value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => throw WrongType("A scalar value was required for text interpolation.")
            };

        private void EnsureStringLimit(string value)
        {
            if (Encoding.UTF8.GetByteCount(value) > _options.MaxStringBytes)
            {
                throw new WorkflowExpressionException("expression_string_limit", "A workflow expression produced a string that exceeded the allowed size.");
            }
        }

        private static EvaluatedValue FromString(string value) => new(false, JsonSerializer.SerializeToElement(value));

        private static EvaluatedValue FromDecimal(decimal value) => new(false, JsonSerializer.SerializeToElement(value));

        private static EvaluatedValue FromInt32(int value) => new(false, JsonSerializer.SerializeToElement(value));

        private static EvaluatedValue FromBoolean(bool value) => new(false, JsonSerializer.SerializeToElement(value));

        private static JsonElement RequirePresent(EvaluatedValue value) => value.IsMissing ? throw InvalidReference() : value.Value;

        private static WorkflowExpressionException InvalidReference() => new(ExecutionErrorCodes.InvalidReference, "A workflow expression reference could not be resolved.");

        private static WorkflowExpressionException WrongType(string message) => new("expression_type_invalid", message);

        private static WorkflowExpressionException InvalidExpression() => new("expression_invalid", "A workflow expression is invalid.");

        private static bool IsValidArity(string name, int count) =>
            name switch
            {
                "default" => count == 2,
                "toString" => count == 1,
                "toNumber" => count == 1,
                "toBoolean" => count == 1,
                "toDateTime" => count == 1,
                "round" => count == 2,
                "formatCurrency" => count == 2,
                "formatDateTime" => count == 3,
                "truncate" => count == 2,
                "uppercase" => count == 1,
                "lowercase" => count == 1,
                "join" => count == 2,
                "count" => count == 1,
                "priceText" => count == 2,
                _ => false
            };

        private IReadOnlyList<TemplatePart> ParseTemplate(string template)
        {
            if (Encoding.UTF8.GetByteCount(template) > _options.MaxTemplateBytes)
            {
                throw new WorkflowExpressionException("expression_template_limit", "A workflow expression template exceeded the allowed size.");
            }

            var parts = new List<TemplatePart>();
            var index = 0;

            while (index < template.Length)
            {
                var start = template.IndexOf("{{", index, StringComparison.Ordinal);

                if (start < 0)
                {
                    parts.Add(new TemplatePart(template[index..], null));
                    break;
                }

                if (start > index)
                {
                    parts.Add(new TemplatePart(template[index..start], null));
                }

                var end = template.IndexOf("}}", start + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw InvalidExpression();
                }

                var expressionText = template[(start + 2)..end].Trim();

                if (expressionText.Length == 0 || expressionText.Contains("{{", StringComparison.Ordinal) || expressionText.Contains("}}", StringComparison.Ordinal))
                {
                    throw InvalidExpression();
                }

                var parser = new Parser(expressionText, _referenceParser, _options.MaxDepth, _options.MaxFunctionArguments);

                parts.Add(new TemplatePart(null, parser.Parse()));
                index = end + 2;
            }

            if (template.Length == 0)
            {
                parts.Add(new TemplatePart(string.Empty, null));
            }

            return parts;
        }

        private static bool TryResolvePath(JsonElement root, string path, out JsonElement value)
        {
            var current = root;

            foreach (var segment in path.Split('.', StringSplitOptions.None))
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(segment, out var propertyValue))
                    {
                        value = default;
                        return false;
                    }

                    current = propertyValue;
                    continue;
                }

                if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var arrayIndex) && arrayIndex >= 0 && arrayIndex < current.GetArrayLength())
                {
                    current = current[arrayIndex];
                    continue;
                }

                value = default;
                return false;
            }

            value = current.Clone();
            return true;
        }

        private sealed record TemplatePart(string? Literal, ExpressionNode? Expression);

        private abstract record ExpressionNode;
        private sealed record LiteralNode(JsonElement Value) : ExpressionNode;
        private sealed record ReferenceNode(WorkflowReference Reference) : ExpressionNode;
        private sealed record FunctionNode(string Name, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;

        private readonly record struct EvaluatedValue(bool IsMissing, JsonElement Value)
        {
            public static EvaluatedValue Missing { get; } = new(true, default);
        }

        private sealed class Parser
        {
            private readonly string _text;
            private readonly IWorkflowReferenceParser _referenceParser;
            private readonly int _maximumDepth;
            private readonly int _maximumFunctionArguments;
            private int _position;

            public Parser(string text, IWorkflowReferenceParser referenceParser, int maximumDepth, int maximumFunctionArguments)
            {
                _text = text;
                _referenceParser = referenceParser;
                _maximumDepth = maximumDepth;
                _maximumFunctionArguments = maximumFunctionArguments;
            }

            public ExpressionNode Parse()
            {
                var node = ParseExpression(1);
                SkipWhitespace();

                if (_position != _text.Length)
                {
                    throw InvalidExpression();
                }

                return node;
            }

            private ExpressionNode ParseExpression(int depth)
            {
                if (depth > _maximumDepth)
                {
                    throw new WorkflowExpressionException("expression_depth_limit", "A workflow expression exceeded the allowed nesting depth.");
                }

                SkipWhitespace();

                if (_position >= _text.Length)
                {
                    throw InvalidExpression();
                }

                if (_text[_position] == '"')
                {
                    return ParseStringLiteral();
                }

                var token = ReadToken();
                SkipWhitespace();

                if (_position < _text.Length && _text[_position] == '(')
                {
                    if (!FunctionNamePattern().IsMatch(token))
                    {
                        throw InvalidExpression();
                    }

                    _position++;
                    var arguments = new List<ExpressionNode>();
                    SkipWhitespace();

                    if (_position < _text.Length && _text[_position] == ')')
                    {
                        _position++;
                    }
                    else
                    {
                        while (true)
                        {
                            arguments.Add(ParseExpression(depth + 1));

                            if (arguments.Count > _maximumFunctionArguments)
                            {
                                throw InvalidExpression();
                            }

                            SkipWhitespace();

                            if (_position >= _text.Length)
                            {
                                throw InvalidExpression();
                            }

                            if (_text[_position] == ')')
                            {
                                _position++;
                                break;
                            }

                            if (_text[_position] != ',')
                            {
                                throw InvalidExpression();
                            }

                            _position++;
                        }
                    }

                    return new FunctionNode(token, arguments);
                }

                if (_referenceParser.TryParse(token, out var reference))
                {
                    return new ReferenceNode(reference);
                }

                if (string.Equals(token, "true", StringComparison.Ordinal))
                {
                    return new LiteralNode(JsonSerializer.SerializeToElement(true));
                }

                if (string.Equals(token, "false", StringComparison.Ordinal))
                {
                    return new LiteralNode(JsonSerializer.SerializeToElement(false));
                }

                if (string.Equals(token, "null", StringComparison.Ordinal))
                {
                    return new LiteralNode(JsonSerializer.SerializeToElement<object?>(null));
                }

                if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    return new LiteralNode(JsonSerializer.SerializeToElement(number));
                }

                throw InvalidExpression();
            }

            private LiteralNode ParseStringLiteral()
            {
                var start = _position;
                _position++;
                var escaped = false;

                while (_position < _text.Length)
                {
                    var character = _text[_position++];

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (character == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (character == '"')
                    {
                        var literal = _text[start.._position];

                        try
                        {
                            var value = JsonSerializer.Deserialize<string>(literal) ?? string.Empty;
                            return new LiteralNode(JsonSerializer.SerializeToElement(value));
                        }
                        catch (JsonException)
                        {
                            throw InvalidExpression();
                        }
                    }
                }

                throw InvalidExpression();
            }

            private string ReadToken()
            {
                SkipWhitespace();
                var start = _position;

                while (_position < _text.Length)
                {
                    var character = _text[_position];

                    if (character is '(' or ')' or ',' || char.IsWhiteSpace(character))
                    {
                        break;
                    }

                    _position++;
                }

                if (_position == start)
                {
                    throw InvalidExpression();
                }

                return _text[start.._position];
            }

            private void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                {
                    _position++;
                }
            }
        }

        [GeneratedRegex(@"\A[A-Za-z][A-Za-z0-9]*\z", RegexOptions.CultureInvariant)]
        private static partial Regex FunctionNamePattern();
    }
}
