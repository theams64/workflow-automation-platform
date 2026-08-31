using Backend.Api.Configuration;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Slack;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SlackWebhookClientTests
    {
        private static readonly Uri FakeWebhook = new("https://hooks.slack.com/services/T000/B000/FAKE_WEBHOOK_SECRET");

        [Fact]
        public async Task SendAsync_ShouldPostJsonWithoutAuthorizationOrCookieHeaders()
        {
            string? body = null;
            var sawAuthorization = false;
            var sawCookie = false;

            var handler = new DelegateHttpMessageHandler(
                async (request, cancellationToken) =>
                {
                    request.Method.Should().Be(HttpMethod.Post);
                    sawAuthorization = request.Headers.Authorization is not null;
                    sawCookie = request.Headers.Contains("Cookie");
                    body = await request.Content!.ReadAsStringAsync(cancellationToken);

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("ok", Encoding.UTF8, "text/plain")
                    };
                });

            using var client = CreateClient(handler);

            await client.SendAsync(Request("Hello"), CancellationToken.None);

            body.Should().Contain("Hello");
            sawAuthorization.Should().BeFalse();
            sawCookie.Should().BeFalse();
        }

        [Fact]
        public async Task SendAsync_ShouldRejectRedirect()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Redirect)
                    {
                        Headers =
                        {
                            Location = new Uri("https://evil.example/")
                        }
                    }));

            using var client = CreateClient(handler);

            var action = () => client.SendAsync(Request("Hello"), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SlackDeliveryException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.SlackDeliveryFailed);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectNonSuccessResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent("details that must not escape")
                    }));

            using var client = CreateClient(handler);

            var action = () => client.SendAsync(Request("Hello"), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SlackDeliveryException>();
            exception.Which.Message.Should().Be("The Slack notification could not be delivered.");
        }

        [Fact]
        public async Task SendAsync_ShouldNotLogWebhookMessageOrRawTransportException()
        {
            const string secretText = "VERY_SECRET_MESSAGE";
            var logger = new ListLogger<SlackWebhookClient>();

            var handler = new DelegateHttpMessageHandler((_, _) =>
                throw new HttpRequestException($"Raw failure containing {FakeWebhook} and {secretText}"));

            using var client = CreateClient(handler, logger);

            var action = () => client.SendAsync(Request(secretText), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SlackDeliveryException>();

            exception.Which.Message.Should().NotContain("FAKE_WEBHOOK_SECRET");
            exception.Which.Message.Should().NotContain(secretText);
            logger.Messages.Should().OnlyContain(message => !message.Contains("FAKE_WEBHOOK_SECRET", StringComparison.Ordinal) && !message.Contains(secretText, StringComparison.Ordinal));
        }

        [Fact]
        public async Task SendAsync_ShouldRejectOversizedResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(new string('x', 2048))
                    }));

            using var client = CreateClient(
                handler,
                options: new SlackDeliveryOptions
                {
                    MaxMessageBytes = 16 * 1024,
                    MaxResponseBytes = 128,
                    ResponseHeadersTimeoutMilliseconds = 1_000,
                    BodyReadTimeoutMilliseconds = 1_000
                });

            var action = () => client.SendAsync(Request("Hello"), CancellationToken.None);

            await action.Should().ThrowAsync<SlackDeliveryException>();
        }

        [Fact]
        public async Task SendAsync_ShouldApplyBodyTimeout()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new SlowReadStream())
                };
                return Task.FromResult(response);
            });

            using var client = CreateClient(
                handler,
                options: new SlackDeliveryOptions
                {
                    MaxMessageBytes = 16 * 1024,
                    MaxResponseBytes = 1024,
                    ResponseHeadersTimeoutMilliseconds = 1_000,
                    BodyReadTimeoutMilliseconds = 10
                });

            var action = () => client.SendAsync(Request("Hello"), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SlackDeliveryException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.RequestTimeout);
        }

        private static SlackWebhookClient CreateClient(HttpMessageHandler handler, ListLogger<SlackWebhookClient>? logger = null, SlackDeliveryOptions? options = null) =>
            new(
                handler,
                Options.Create(options ?? new SlackDeliveryOptions()),
                Options.Create(new SafeHttpOptions()),
                new NoopOutboundConcurrencyLimiter(),
                logger ?? new ListLogger<SlackWebhookClient>()
            );

        private static SlackWebhookRequest Request(string text) =>
            new(
                UserId: 7,
                WorkflowId: 1,
                ConnectionId: Guid.NewGuid(),
                WebhookUri: FakeWebhook,
                Text: text
            );
    }
}
