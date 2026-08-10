using Backend.Api.Models.Validation;
using System.Text;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Execution
{
    public class NormalizedStepOutput
    {
        private NormalizedStepOutput(JsonElement value, string json)
        {
            Value = value;
            Json = json;
        }

        public JsonElement Value { get; }
        public string Json { get; }

        public static NormalizedStepOutput Empty { get; } = FromJson("{}");

        public static NormalizedStepOutput FromJson(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            var byteCount = Encoding.UTF8.GetByteCount(json);
            if (byteCount > WorkflowLimits.NormalizedOutputMaxBytes)
            {
                throw new NormalizedOutputTooLargeException();
            }

            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });

            var canonicalJson = JsonSerializer.Serialize(document.RootElement);
            if (Encoding.UTF8.GetByteCount(canonicalJson) > WorkflowLimits.NormalizedOutputMaxBytes)
            {
                throw new NormalizedOutputTooLargeException();
            }

            return new NormalizedStepOutput(document.RootElement.Clone(), canonicalJson);
        }

        public static NormalizedStepOutput FromValue<T>(T value) => FromJson(JsonSerializer.Serialize(value));
    }

    public sealed class NormalizedOutputTooLargeException : Exception
    {
        public NormalizedOutputTooLargeException() : base("The normalized step output exceeded the configured limit.") {}
    }
}
