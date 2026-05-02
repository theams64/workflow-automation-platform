using System.Text.Json;

namespace Backend.Api.Services.Common
{
    public sealed class JsonValidationHelper : IJsonValidationHelper
    {
        public bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                JsonDocument.Parse(json);
                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}
