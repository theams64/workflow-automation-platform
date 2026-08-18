using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using System.Text;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Http
{
    public static class BoundedJsonValidator
    {
        public static JsonElement Parse(ReadOnlyMemory<byte> utf8Json, SafeHttpOptions options)
        {
            try
            {
                using var document = JsonDocument.Parse(utf8Json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = options.MaxJsonDepth
                });

                var tokenCount = 0;
                ValidateElement(document.RootElement, options, ref tokenCount);

                return document.RootElement.Clone();
            }
            catch (SafeHttpException)
            {
                throw;
            }
            catch (JsonException)
            { 
                throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response was not valid bounded JSON.");
            }
        }

        private static void ValidateElement(JsonElement element, SafeHttpOptions options, ref int tokenCount)
        {
            IncrementToken(options, ref tokenCount);

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        var propertyCount = 0;

                        foreach (var property in element.EnumerateObject())
                        {
                            propertyCount++;

                            if (propertyCount > options.MaxJsonPropertiesPerObject)
                            {
                                throw InvalidComplexity();
                            }

                            if (Encoding.UTF8.GetByteCount(property.Name) > options.MaxJsonStringBytes)
                            {
                                throw InvalidComplexity();
                            }

                            IncrementToken(options, ref tokenCount);
                            ValidateElement(property.Value, options, ref tokenCount);
                        }

                        break;
                    }

                case JsonValueKind.Array:
                    {
                        var itemCount = 0;

                        foreach (var item in element.EnumerateArray())
                        {
                            itemCount++;

                            if (itemCount > options.MaxJsonArrayItems)
                            {
                                throw InvalidComplexity();
                            }

                            ValidateElement(item, options, ref tokenCount);
                        }

                        break;
                    }

                case JsonValueKind.String:
                    if (Encoding.UTF8.GetByteCount(
                            element.GetString() ?? string.Empty) >
                        options.MaxJsonStringBytes)
                    {
                        throw InvalidComplexity();
                    }
                    break;
            }
        }

        private static void IncrementToken(SafeHttpOptions options, ref int tokenCount)
        {
            tokenCount++;

            if (tokenCount > options.MaxJsonTokenCount)
            {
                throw InvalidComplexity();
            }
        }

        private static SafeHttpException InvalidComplexity() => new(ExecutionErrorCodes.InvalidResponse, "The HTTP response JSON exceeded the allowed complexity.");
    }
}
