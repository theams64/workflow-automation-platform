using Backend.Api.Configuration;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SafeOutboundHttpClientTests
    {
        [Fact]
        public async Task SendAsync_ShouldReturnOnlyBoundedSelectedResponseData()
        {
            var sawAuthorization = false;
            var sawCookie = false;
            var sawForwarded = false;
            var sawTraceParent = false;

            var handler = new DelegateHttpMessageHandler((request, _) =>
            {
                sawAuthorization = request.Headers.Authorization is not null;
                sawCookie = request.Headers.Contains("Cookie");
                sawForwarded = request.Headers.Contains("X-Forwarded-For");
                sawTraceParent = request.Headers.Contains("traceparent");

                var response = SafeHttpTestHelpers.JsonResponse("""{"value":42}""");
                response.Headers.ETag = new EntityTagHeaderValue("\"abc\""); 
                response.Headers.TryAddWithoutValidation("Set-Cookie", "session=secret");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);
            var policy = SafeHttpTestHelpers.Policy(selectedHeaders: new HashSet<string>(["etag"], StringComparer.OrdinalIgnoreCase));

            var result = await client.SendAsync(SafeHttpTestHelpers.Request(), policy, CancellationToken.None);

            result.StatusCode.Should().Be(200);
            result.Body.GetProperty("value").GetInt32().Should().Be(42);
            result.SelectedHeaders.Should().ContainKey("etag");
            result.SelectedHeaders.Should().NotContainKey("set-cookie");
            result.ReceivedBytes.Should().BeGreaterThan(0);

            sawAuthorization.Should().BeFalse();
            sawCookie.Should().BeFalse();
            sawForwarded.Should().BeFalse();
            sawTraceParent.Should().BeFalse();
        }

        [Fact]
        public async Task SendAsync_ShouldRejectRedirectResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://evil.example/");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.RedirectNotAllowed);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectNonSuccessResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(SafeHttpTestHelpers.JsonResponse("{}", HttpStatusCode.BadRequest)));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.HttpNonSuccessStatus);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectNonJsonContentType()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("hello")
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectOversizedResponseHeaders()
        {
            var options = new SafeHttpOptions
            {
                MaxResponseHeaderBytes = 32
            };

            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = SafeHttpTestHelpers.JsonResponse("{}");
                response.Headers.TryAddWithoutValidation("X-Large", new string('x', 128));
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler, options);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ResponseHeadersTooLarge);
        }

        [Fact]
        public async Task SendAsync_ShouldMapTransportHeaderLimitWithoutInspectingMessage()
        {
            const string secret = "SHOULD_NOT_ESCAPE";

            var handler = new DelegateHttpMessageHandler((_, _) => throw new HttpRequestException(HttpRequestError.ConfigurationLimitExceeded, secret));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ResponseHeadersTooLarge);
            exception.Which.Message.Should().NotContain(secret);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectOversizedWireBodyBeforeParsing()
        {
            var options = new SafeHttpOptions
            {
                MaxCompressedResponseBytes = 64
            };

            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[128])
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler, options);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ResponseTooLarge);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectOversizedDecompressedBody()
        {
            var options = new SafeHttpOptions
            {
                MaxCompressedResponseBytes = 1024,
                MaxDecompressedResponseBytes = 64
            };

            var largeJson = "{\"value\":\"" + new string('a', 500) + "\"}";

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(SafeHttpTestHelpers.GzipJsonResponse(largeJson)));

            using var client = SafeHttpTestHelpers.CreateClient(handler, options);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(maximumResponseBytes: 64), SafeHttpTestHelpers.Policy(maximumResponseBytes: 64), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ResponseTooLarge);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectMalformedJson()
        {
            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(SafeHttpTestHelpers.JsonResponse("{not-json}")));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectJsonAboveDepthLimit()
        {
            var options = new SafeHttpOptions
            {
                MaxJsonDepth = 2
            };

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(SafeHttpTestHelpers.JsonResponse("""{"a":{"b":{"c":1}}}""")));

            using var client = SafeHttpTestHelpers.CreateClient(handler, options);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public async Task SendAsync_ShouldRejectJsonAboveArrayLimit()
        {
            var options = new SafeHttpOptions
            {
                MaxJsonArrayItems = 2
            };
             
            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(SafeHttpTestHelpers.JsonResponse("[1,2,3]")));

            using var client = SafeHttpTestHelpers.CreateClient(handler, options);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public async Task SendAsync_ShouldApplyResponseHeaderTimeout()
        {
            var handler = new DelegateHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return SafeHttpTestHelpers.JsonResponse("{}");
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);
            var policy = SafeHttpTestHelpers.Policy(headerTimeoutMilliseconds: 25);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), policy, CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.RequestTimeout);
        }

        [Fact]
        public async Task SendAsync_ShouldApplyBodyReadTimeout()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new SlowReadStream())
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);
            var policy = SafeHttpTestHelpers.Policy(bodyTimeoutMilliseconds: 25);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), policy, CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.RequestTimeout);
        }

        [Fact]
        public async Task SendAsync_ShouldPreserveCallerCancellation()
        {
            var handler = new DelegateHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return SafeHttpTestHelpers.JsonResponse("{}");
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);
            using var cancellation = new CancellationTokenSource(25);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(headerTimeoutMilliseconds: 1_000), cancellation.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task SendAsync_ShouldPreserveSanitizedConnectorErrorCode()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                throw new HttpRequestException("transport wrapper", new SafeHttpException(ExecutionErrorCodes.ConnectionTimeout, "The outbound HTTP connection timed out.")));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ConnectionTimeout);
        }

        [Fact]
        public async Task SendAsync_ShouldNotLogRawTransportExceptionOrQueryValue()
        {
            const string secret = "VERY_SECRET_VALUE";
            var logger = new ListLogger<SafeOutboundHttpClient>();

            var handler = new DelegateHttpMessageHandler((_, _) => throw new HttpRequestException($"Raw transport failure containing {secret}"));

            using var client = SafeHttpTestHelpers.CreateClient(handler, logger: logger);

            var request = SafeHttpTestHelpers.Request(
                query: new Dictionary<string, string?>
                {
                    ["q"] = secret
                });

            var action = () => client.SendAsync(request, SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();

            exception.Which.Message.Should().NotContain(secret);
            logger.Messages.Should().OnlyContain(message => !message.Contains(secret, StringComparison.Ordinal));
        }

        [Fact]
        public async Task SendAsync_ShouldRejectControlCharactersInSelectedHeader()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
            {
                var response = SafeHttpTestHelpers.JsonResponse("{}");
                response.Headers.TryAddWithoutValidation("ETag", "ok\r\nInjected: yes");
                return Task.FromResult(response);
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);
            var policy = SafeHttpTestHelpers.Policy(selectedHeaders: new HashSet<string>(["etag"], StringComparer.OrdinalIgnoreCase));

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), policy, CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
        }

        [Fact]
        public async Task SendAsync_ShouldAdvertiseOnlySupportedContentEncodings()
        {
            string[]? advertisedEncodings = null;

            var handler = new DelegateHttpMessageHandler((request, _) =>
            {
                advertisedEncodings = request.Headers.AcceptEncoding
                    .Select(value => value.Value)
                    .ToArray();

                return Task.FromResult(SafeHttpTestHelpers.JsonResponse("""{"value":42}"""));
            });

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            await client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            advertisedEncodings.Should().NotBeNull();
            advertisedEncodings.Should().Equal("gzip", "br");
            advertisedEncodings.Should().NotContain("deflate");
        }

        [Fact]
        public async Task SendAsync_ShouldDecompressGzipResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(SafeHttpTestHelpers.GzipJsonResponse("""{"temperature":72.5,"condition":"clear"}""")));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var result = await client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            result.StatusCode.Should().Be(200);
            result.Body.GetProperty("temperature").GetDouble().Should().Be(72.5);
            result.Body.GetProperty("condition").GetString().Should().Be("clear");
        }

        [Fact]
        public async Task SendAsync_ShouldDecompressBrotliResponse()
        {
            var handler = new DelegateHttpMessageHandler((_, _) =>
                Task.FromResult(SafeHttpTestHelpers.BrotliJsonResponse("""{"temperature":72.5,"condition":"clear"}""")));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var result = await client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None); 

            result.StatusCode.Should().Be(200);
            result.Body.GetProperty("temperature").GetDouble().Should().Be(72.5);
            result.Body.GetProperty("condition").GetString().Should().Be("clear");
        }

        [Fact]
        public async Task SendAsync_ShouldRejectDeflateResponseAsUnsupported()
        {
            var response = SafeHttpTestHelpers.EncodedResponse("deflate", Encoding.UTF8.GetBytes("""{"value":42}"""));

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();

            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
            exception.Which.Message.Should().Be("The HTTP response used an unsupported content encoding.");
        }

        [Fact]
        public async Task SendAsync_ShouldRejectMalformedGzipResponse()
        {
            var response = SafeHttpTestHelpers.EncodedResponse("gzip", Encoding.UTF8.GetBytes("""{"this":"is not actually gzip"}"""));

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();

            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
            exception.Which.Message.Should().Be("The HTTP response body was invalid.");
        }

        [Fact]
        public async Task SendAsync_ShouldRejectMalformedBrotliResponse()
        {
            var response = SafeHttpTestHelpers.EncodedResponse("br", [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();

            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
            exception.Which.Message.Should().Be("The HTTP response body was invalid.");
        }

        [Fact]
        public async Task SendAsync_ShouldRejectMultipleContentEncodings()
        {
            var response = SafeHttpTestHelpers.JsonResponse("""{"value":42}""");

            response.Content.Headers.ContentEncoding.Add("gzip");
            response.Content.Headers.ContentEncoding.Add("br");

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

            using var client = SafeHttpTestHelpers.CreateClient(handler);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            var exception = await action.Should().ThrowAsync<SafeHttpException>();

            exception.Which.Code.Should().Be(ExecutionErrorCodes.InvalidResponse);
            exception.Which.Message.Should().Be("The HTTP response used an unsupported content encoding.");
        }

        [Fact]
        public async Task SendAsync_ShouldLogEncodingWhenDecompressionFails()
        {
            var logger = new ListLogger<SafeOutboundHttpClient>();

            var response = SafeHttpTestHelpers.EncodedResponse("gzip", Encoding.UTF8.GetBytes("""{"not":"actually compressed"}"""));

            var handler = new DelegateHttpMessageHandler((_, _) => Task.FromResult(response));

            using var client = SafeHttpTestHelpers.CreateClient(handler, logger: logger);

            var action = () => client.SendAsync(SafeHttpTestHelpers.Request(), SafeHttpTestHelpers.Policy(), CancellationToken.None);

            await action.Should().ThrowAsync<SafeHttpException>();

            logger.Messages.Should().Contain(message =>
                message.Contains("Failed to decompress HTTP response", StringComparison.Ordinal) &&
                message.Contains("gzip", StringComparison.OrdinalIgnoreCase));
        }
    }
}
