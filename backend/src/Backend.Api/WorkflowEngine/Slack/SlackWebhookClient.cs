using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Slack
{
    public sealed class SlackWebhookClient : ISlackWebhookClient, IDisposable
    {
        private readonly SlackDeliveryOptions _options;
        private readonly SafeHttpOptions _safeHttpOptions;
        private readonly IOutboundConcurrencyLimiter _concurrencyLimiter;
        private readonly ILogger<SlackWebhookClient> _logger;
        private readonly HttpClient _httpClient;

        public SlackWebhookClient(IOptions<SlackDeliveryOptions> options, IOptions<SafeHttpOptions> safeHttpOptions, IPublicNetworkConnector connector, IOutboundConcurrencyLimiter concurrencyLimiter, ILogger<SlackWebhookClient> logger)
            : this(SafeHttpMessageHandlerFactory.Create(safeHttpOptions.Value, connector), options, safeHttpOptions, concurrencyLimiter, logger)
        {
        }

        public SlackWebhookClient(HttpMessageHandler handler, IOptions<SlackDeliveryOptions> options, IOptions<SafeHttpOptions> safeHttpOptions, IOutboundConcurrencyLimiter concurrencyLimiter, ILogger<SlackWebhookClient> logger)
        {
            _options = options.Value;
            _safeHttpOptions = safeHttpOptions.Value;
            _concurrencyLimiter = concurrencyLimiter;
            _logger = logger;
            _httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task SendAsync(SlackWebhookRequest request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.WebhookUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(request.WebhookUri.IdnHost, "hooks.slack.com", StringComparison.OrdinalIgnoreCase) ||
                request.WebhookUri.Port != 443 ||
                !string.IsNullOrEmpty(request.WebhookUri.UserInfo) ||
                !string.IsNullOrEmpty(request.WebhookUri.Query) ||
                !string.IsNullOrEmpty(request.WebhookUri.Fragment))
            {
                throw DeliveryFailure();
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(new { text = request.Text });

            if (Encoding.UTF8.GetByteCount(request.Text) > _options.MaxMessageBytes || payload.Length > _options.MaxMessageBytes + 256)
            {
                throw new SlackDeliveryException(ExecutionErrorCodes.MessageSizeExceeded, "The Slack message exceeded the allowed size.");
            }

            // Reuse the existing process-local global/user/workflow/origin gates.
            // The synthetic request is used only as a limiter key and is never sent.
            var limiterRequest = new SafeHttpRequest(request.UserId, request.WorkflowId, request.WebhookUri.IdnHost, HttpMethod.Get, "/", new Dictionary<string, string?>(StringComparer.Ordinal), 1);

            await using var lease = await _concurrencyLimiter.AcquireAsync(limiterRequest, cancellationToken);

            using var message = new HttpRequestMessage(HttpMethod.Post, request.WebhookUri);
            message.Content = new ByteArrayContent(payload);
            message.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };

            using var headerTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.ResponseHeadersTimeoutMilliseconds));
            using var headerLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headerTimeout.Token);

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, headerLinked.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (headerTimeout.IsCancellationRequested)
            {
                LogFailure(request, ExecutionErrorCodes.RequestTimeout);
                throw new SlackDeliveryException(ExecutionErrorCodes.RequestTimeout, "The Slack request timed out.");
            }
            catch (HttpRequestException exception) when (FindSafeHttpException(exception) is not null)
            {
                LogFailure(request, ExecutionErrorCodes.SlackDeliveryFailed);
                throw DeliveryFailure();
            }
            catch (HttpRequestException)
            {
                LogFailure(request, ExecutionErrorCodes.SlackDeliveryFailed);
                throw DeliveryFailure();
            }

            using (response)
            {
                ValidateResponseHeaders(response);

                var statusCode = (int)response.StatusCode;

                if (statusCode is >= 300 and <= 399)
                {
                    LogFailure(request, ExecutionErrorCodes.SlackDeliveryFailed);
                    throw DeliveryFailure();
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogFailure(request, ExecutionErrorCodes.SlackDeliveryFailed);
                    throw DeliveryFailure();
                }

                using var bodyTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(_options.BodyReadTimeoutMilliseconds));
                using var bodyLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bodyTimeout.Token);

                try
                {
                    await ReadAndDiscardBoundedResponseAsync(response, bodyLinked.Token);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (bodyTimeout.IsCancellationRequested)
                {
                    LogFailure(request, ExecutionErrorCodes.RequestTimeout);
                    throw new SlackDeliveryException(ExecutionErrorCodes.RequestTimeout, "The Slack response timed out.");
                }
                catch (SlackDeliveryException)
                {
                    throw;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    LogFailure(request, ExecutionErrorCodes.SlackDeliveryFailed);
                    throw DeliveryFailure();
                }

                _logger.LogInformation("Slack delivery succeeded for connection {ConnectionId}, workflow {WorkflowId}, status {StatusCode}.", request.ConnectionId, request.WorkflowId, statusCode);
            }
        }

        private void ValidateResponseHeaders(HttpResponseMessage response)
        {
            long totalBytes = 0;

            foreach (var header in response.Headers)
            {
                totalBytes += CountHeaderBytes(header.Key, header.Value);
            }

            if (response.Content is not null)
            {
                foreach (var header in response.Content.Headers)
                {
                    totalBytes += CountHeaderBytes(header.Key, header.Value);
                }
            }

            if (totalBytes > _safeHttpOptions.MaxResponseHeaderBytes)
            {
                throw DeliveryFailure();
            }
        }

        private async Task ReadAndDiscardBoundedResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content is null)
            {
                return;
            }

            if (response.Content.Headers.ContentLength is long length && length > _options.MaxResponseBytes)
            {
                throw DeliveryFailure();
            }

            await using var raw = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var bounded = new SizeLimitedReadStream(raw, _options.MaxResponseBytes, ExecutionErrorCodes.SlackDeliveryFailed);
            await using var sink = new MemoryStream();

            try
            {
                await bounded.CopyToAsync(sink, cancellationToken);
            }
            catch (SafeHttpException)
            {
                throw DeliveryFailure();
            }
        }

        private static long CountHeaderBytes(string name, IEnumerable<string> values)
        {
            long bytes = Encoding.UTF8.GetByteCount(name) + 4;

            foreach (var value in values)
            {
                bytes += Encoding.UTF8.GetByteCount(value) + 2;
            }

            return bytes;
        }

        private void LogFailure(SlackWebhookRequest request, string errorCode)
        {
            _logger.LogWarning("Slack delivery failed for connection {ConnectionId}, workflow {WorkflowId}, code {ErrorCode}.", request.ConnectionId, request.WorkflowId, errorCode);
        }

        private static SafeHttpException? FindSafeHttpException(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SafeHttpException safe)
                {
                    return safe;
                }
            }

            return null;
        }

        private static SlackDeliveryException DeliveryFailure()
        {
            return new SlackDeliveryException(ExecutionErrorCodes.SlackDeliveryFailed, "The Slack notification could not be delivered.");
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
