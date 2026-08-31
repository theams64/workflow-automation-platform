using Backend.Api.WorkflowEngine.Slack;
using FluentAssertions;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SlackWebhookUriPolicyTests
    {
        [Theory]
        [InlineData("http://hooks.slack.com/services/T/B/X")]
        [InlineData("https://evil.example/services/T/B/X")]
        [InlineData("https://hooks.slack.com.evil.example/services/T/B/X")]
        [InlineData("https://hooks.slack.com@evil.example/services/T/B/X")]
        [InlineData("https://hooks.slack.com/services/T/B/X?secret=1")]
        [InlineData("https://hooks.slack.com/services/T/B/X#fragment")]
        [InlineData("https://hooks.slack.com/not-services/T/B/X")]
        [InlineData("https://hooks.slack.com/services/T/B")]
        public void TryCreate_ShouldRejectInvalidOrMismatchedWebhookUris(string value)
        {
            SlackWebhookUriPolicy.TryCreate(value, SlackWebhookUriPolicy.CanonicalOrigin, out _).Should().BeFalse();
        }

        [Fact]
        public void TryCreate_ShouldAcceptExpectedSlackWebhookShape()
        {
            var result = SlackWebhookUriPolicy.TryCreate("https://hooks.slack.com/services/T000/B000/FAKE_SECRET", SlackWebhookUriPolicy.CanonicalOrigin, out var uri);

            result.Should().BeTrue();
            uri.IdnHost.Should().Be("hooks.slack.com");
        }
    }
}
