using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SafeHttpUriBuilderTests
    {
        [Theory]
        [InlineData("https://evil.example/path")]
        [InlineData("//evil.example/path")]
        [InlineData("/forecast/../admin")]
        [InlineData("/forecast/%2e%2e/admin")]
        [InlineData("/forecast/%252e%252e/admin")]
        [InlineData("/forecast\\admin")]
        [InlineData("/forecast#fragment")]
        [InlineData("/forecast?other=value")]
        [InlineData("/forecast/\r\nheader")]
        public void Build_ShouldRejectDangerousPaths(string path)
        {
            var action = () => SafeHttpUriBuilder.Build(
                SafeHttpTestHelpers.Request(path),
                SafeHttpTestHelpers.Policy(pathPrefixes: ["/"]),
                SafeHttpTestHelpers.Options());

            action.Should().Throw<SafeHttpException>().Which.Code.Should().BeOneOf(ExecutionErrorCodes.RequestUriInvalid, ExecutionErrorCodes.PathNotAllowed);
        }

        [Fact]
        public void Build_ShouldRejectPathPrefixConfusion()
        {
            var action = () => SafeHttpUriBuilder.Build(
                SafeHttpTestHelpers.Request("/forecast-admin"),
                SafeHttpTestHelpers.Policy(pathPrefixes: ["/forecast"]),
                SafeHttpTestHelpers.Options());

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.PathNotAllowed);
        }

        [Fact]
        public void Build_ShouldRejectUnsupportedMethod()
        {
            var action = () => SafeHttpUriBuilder.Build(
                SafeHttpTestHelpers.Request(method: HttpMethod.Post),
                SafeHttpTestHelpers.Policy(),
                SafeHttpTestHelpers.Options());

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.MethodNotAllowed);
        }

        [Fact]
        public void Build_ShouldRejectUnsupportedQueryParameter()
        {
            var request = SafeHttpTestHelpers.Request(
                query: new Dictionary<string, string?>
                {
                    ["admin"] = "true"
                });

            var action = () => SafeHttpUriBuilder.Build(request, SafeHttpTestHelpers.Policy(), SafeHttpTestHelpers.Options());

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.QueryParameterNotAllowed);
        }

        [Fact]
        public void Build_ShouldRejectControlCharactersInQueryValues()
        {
            var request = SafeHttpTestHelpers.Request(
                query: new Dictionary<string, string?>
                {
                    ["q"] = "ok\r\nX-Injected: yes"
                });

            var action = () => SafeHttpUriBuilder.Build(request, SafeHttpTestHelpers.Policy(), SafeHttpTestHelpers.Options());

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.RequestUriInvalid);
        }

        [Fact]
        public void Build_ShouldEncodeQuerySyntaxAsData()
        {
            var request = SafeHttpTestHelpers.Request(
                query: new Dictionary<string, string?>
                {
                    ["q"] = "a&admin=true"
                });

            var uri = SafeHttpUriBuilder.Build(request, SafeHttpTestHelpers.Policy(), SafeHttpTestHelpers.Options());

            uri.Host.Should().Be("api.example.com");
            uri.Query.Should().Be("?q=a%26admin%3Dtrue");
        }
    }
}
