using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using System.Text;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class BoundedJsonValidatorTests
    {
        [Fact]
        public void Parse_ShouldRejectPropertyCountLimit()
        {
            var options = new SafeHttpOptions
            {
                MaxJsonPropertiesPerObject = 1
            };

            var action = () => BoundedJsonValidator.Parse(Encoding.UTF8.GetBytes("""{"a":1,"b":2}"""), options);

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public void Parse_ShouldRejectStringByteLimit()
        {
            var options = new SafeHttpOptions
            {
                MaxJsonStringBytes = 4
            };

            var action = () => BoundedJsonValidator.Parse(Encoding.UTF8.GetBytes("""{"v":"long-value"}"""), options);

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public void Parse_ShouldRejectTokenLimit()
        {
            var options = new SafeHttpOptions
            {
                MaxJsonTokenCount = 2
            };

            var action = () => BoundedJsonValidator.Parse(Encoding.UTF8.GetBytes("""{"a":1}"""), options);

            action.Should().Throw<SafeHttpException>().Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }
    }
}
