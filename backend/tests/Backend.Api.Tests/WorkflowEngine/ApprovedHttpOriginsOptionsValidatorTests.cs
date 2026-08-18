using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class ApprovedHttpOriginsOptionsValidatorTests
    {
        [Theory]
        [InlineData("http://api.example.com")]
        [InlineData("https://user:password@api.example.com")]
        [InlineData("https://api.example.com/path")]
        [InlineData("https://api.example.com?x=1")]
        [InlineData("https://127.0.0.1")]
        [InlineData("https://10.0.0.1")]
        public void Validate_ShouldRejectUnsafeBaseUris(string baseUri)
        {
            var validator = new ApprovedHttpOriginsOptionsValidator(Options.Create(new SafeHttpOptions()));

            var result = validator.Validate(null, OptionsFor(baseUri));

            result.Failed.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldRejectNonGetMethods()
        {
            var validator = new ApprovedHttpOriginsOptionsValidator(Options.Create(new SafeHttpOptions()));
            var options = OptionsFor("https://api.example.com");
            options.Origins["test"] = new ApprovedHttpOriginOptions
            {
                BaseUri = "https://api.example.com",
                AllowedMethods = ["GET", "POST"],
                AllowedPathPrefixes = ["/forecast"]
            };

            var result = validator.Validate(null, options);

            result.Failed.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldRejectEncodedTraversalPrefix()
        {
            var validator = new ApprovedHttpOriginsOptionsValidator(Options.Create(new SafeHttpOptions()));
            var options = OptionsFor("https://api.example.com");
            options.Origins["test"] = new ApprovedHttpOriginOptions
            {
                BaseUri = "https://api.example.com",
                AllowedMethods = ["GET"],
                AllowedPathPrefixes = ["/forecast/%2e%2e/admin"]
            };

            var result = validator.Validate(null, options);

            result.Failed.Should().BeTrue();
        }

        private static ApprovedHttpOriginsOptions OptionsFor(string baseUri) => new()
        {
            Origins = new Dictionary<string, ApprovedHttpOriginOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["test"] = new()
                {
                    BaseUri = baseUri,
                    AllowedMethods = ["GET"],
                    AllowedPathPrefixes = ["/forecast"]
                }
            }
        };
    }
}
