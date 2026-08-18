using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed class SafeOutboundHttpClient : ISafeOutboundHttpClient, IDisposable
    {
        private readonly SafeHttpOptions _options;
        private readonly IOutboundConcurrencyLimiter _concurrencyLimiter;
        private readonly ILogger<SafeOutboundHttpClient> _logger;
        private readonly HttpClient _httpClient;

        public SafeOutboundHttpClient(IOptions<SafeHttpOptions> options, IPublicNetworkConnector connector, IOutboundConcurrencyLimiter concurrencyLimiter, ILogger<SafeOutboundHttpClient> logger)
            : this(SafeHttpMessageHandlerFactory.Create(options.Value, connector), options, concurrencyLimiter, logger)
        {
        }

        public SafeOutboundHttpClient(HttpMessageHandler handler, IOptions<SafeHttpOptions> options, IOutboundConcurrencyLimiter concurrencyLimiter, ILogger<SafeOutboundHttpClient> logger)
        {
            _options = options.Value;
            _concurrencyLimiter = concurrencyLimiter;
            _logger = logger;
            _httpClient = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        public async Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, OutboundRequestPolicy policy, CancellationToken cancellationToken)
        {
            var uri = SafeHttpUriBuilder.Build(request, policy, _options);

            await using var lease = await _concurrencyLimiter.AcquireAsync(request, cancellationToken);

            using var message = new HttpRequestMessage(request.Method, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

            using var headerTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(policy.ResponseHeadersTimeoutMilliseconds));
            using var headerLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, headerTimeout.Token);

            HttpResponseMessage response;

            try
            {
                response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, headerLinked.Token);
            }
            catch (HttpRequestException exception) when (FindSafeHttpException(exception) is { } safeException)
            {
                LogFailure(request, safeException.Code);
                throw safeException;
            }
            catch (HttpRequestException exception) when (exception.HttpRequestError == HttpRequestError.ConfigurationLimitExceeded)
            {
                LogFailure(request, ExecutionErrorCodes.ResponseHeadersTooLarge);

                throw new SafeHttpException(ExecutionErrorCodes.ResponseHeadersTooLarge, "The HTTP response headers exceeded the allowed size.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (headerTimeout.IsCancellationRequested)
            {
                LogFailure(request, ExecutionErrorCodes.RequestTimeout);

                throw new SafeHttpException(ExecutionErrorCodes.RequestTimeout, "The HTTP response headers timed out.");
            }
            catch (HttpRequestException)
            {
                LogFailure(request, ExecutionErrorCodes.HttpRequestFailed);

                throw new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP request failed.");
            }

            using (response)
            {
                ValidateResponseHeaders(response);

                var statusCode = (int)response.StatusCode;

                if (statusCode is >= 300 and <= 399)
                {
                    LogFailure(request, ExecutionErrorCodes.RedirectNotAllowed);

                    throw new SafeHttpException(ExecutionErrorCodes.RedirectNotAllowed, "The HTTP origin returned a redirect, which is not allowed.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    LogFailure(request, ExecutionErrorCodes.HttpNonSuccessStatus);

                    throw new SafeHttpException(ExecutionErrorCodes.HttpNonSuccessStatus, "The HTTP origin returned a non-success status code.");
                }

                if (response.Content is null)
                {
                    throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response did not contain JSON content.");
                }

                var contentType = ValidateContentType(response.Content.Headers.ContentType);

                using var bodyTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(policy.BodyReadTimeoutMilliseconds));
                using var bodyLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bodyTimeout.Token);

                byte[] bodyBytes;

                try
                {
                    bodyBytes = await ReadBoundedBodyAsync(response, request.MaximumResponseBytes, bodyLinked.Token);
                }
                catch (SafeHttpException)
                {
                    throw;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (bodyTimeout.IsCancellationRequested)
                {
                    LogFailure(request, ExecutionErrorCodes.RequestTimeout);

                    throw new SafeHttpException(ExecutionErrorCodes.RequestTimeout, "The HTTP response body timed out.");
                }
                catch (InvalidDataException)
                {
                    LogFailure(request, ExecutionErrorCodes.InvalidResponse);

                    throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse,"The HTTP response body was invalid.");
                }
                catch (HttpRequestException exception) when (FindSafeHttpException(exception) is { } safeException)
                {
                    LogFailure(request, safeException.Code);
                    throw safeException;
                }
                catch (HttpRequestException)
                {
                    LogFailure(request, ExecutionErrorCodes.HttpRequestFailed);

                    throw new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP response body could not be read.");
                }
                catch (IOException)
                {
                    LogFailure(request, ExecutionErrorCodes.HttpRequestFailed);

                    throw new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP response body could not be read.");
                }

                var body = BoundedJsonValidator.Parse(bodyBytes, _options);

                var selectedHeaders = SelectHeaders(response, policy.SelectedResponseHeaders);

                _logger.LogInformation("Safe HTTP request succeeded for origin {OriginId}, workflow {WorkflowId}, status {StatusCode}, received bytes {ReceivedBytes}.", request.OriginId, request.WorkflowId, statusCode, bodyBytes.Length);

                return new SafeHttpResponse(statusCode, contentType, body, selectedHeaders, bodyBytes.Length);
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

            if (totalBytes > _options.MaxResponseHeaderBytes)
            {
                throw new SafeHttpException(ExecutionErrorCodes.ResponseHeadersTooLarge, "The HTTP response headers exceeded the allowed size.");
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

        private static string ValidateContentType(MediaTypeHeaderValue? contentType)
        {
            var mediaType = contentType?.MediaType;

            if (string.IsNullOrWhiteSpace(mediaType) || (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) && !mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response must use a JSON content type.");
            }

            var charset = contentType?.CharSet?.Trim().Trim('"');

            if (!string.IsNullOrEmpty(charset) && !string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase))
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response must use UTF-8 JSON.");
            }

            return mediaType;
        }

        private async Task<byte[]> ReadBoundedBodyAsync(HttpResponseMessage response, int maximumResponseBytes, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength is long length && length > _options.MaxCompressedResponseBytes)
            {
                throw new SafeHttpException(ExecutionErrorCodes.ResponseTooLarge, "The HTTP response exceeded the allowed size.");
            }

            var encodings = response.Content.Headers.ContentEncoding
                .Select(value => value.Trim().ToLowerInvariant())
                .ToArray();

            if (encodings.Length > 1)
            {
                throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response used an unsupported content encoding.");
            }

            var raw = await response.Content.ReadAsStreamAsync(cancellationToken);

            await using var compressed = new SizeLimitedReadStream(raw, _options.MaxCompressedResponseBytes);

            Stream decoded = encodings.SingleOrDefault() switch
            {
                null => compressed,
                "" => compressed,
                "identity" => compressed,
                "gzip" => new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: false),
                "br" => new BrotliStream(compressed, CompressionMode.Decompress, leaveOpen: false),
                "deflate" => new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false),
                _ => throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response used an unsupported content encoding.")
            };

            await using (decoded)
            await using (var boundedDecoded = new SizeLimitedReadStream(decoded, Math.Min(maximumResponseBytes, _options.MaxDecompressedResponseBytes)))
            await using (var buffer = new MemoryStream())
            {
                await boundedDecoded.CopyToAsync(buffer, cancellationToken);

                return buffer.ToArray();
            }
        }

        private static IReadOnlyDictionary<string, string> SelectHeaders(HttpResponseMessage response, IReadOnlySet<string> selectedNames)
        {
            var selected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in selectedNames)
            {
                IEnumerable<string>? values = null;

                if (response.Headers.TryGetValues(name, out var responseValues))
                {
                    values = responseValues;
                }
                else if (response.Content.Headers.TryGetValues(name, out var contentValues))
                {
                    values = contentValues;
                }

                if (values is null)
                {
                    continue;
                }

                var value = string.Join(", ", values);

                if (value.Any(char.IsControl))
                {
                    throw new SafeHttpException(ExecutionErrorCodes.InvalidResponse, "The HTTP response contained an invalid selected header.");
                }

                selected[name.ToLowerInvariant()] = value;
            }

            return selected;
        }

        private void LogFailure(SafeHttpRequest request, string errorCode)
        {
            _logger.LogWarning("Safe HTTP request failed for origin {OriginId}, workflow {WorkflowId}, code {ErrorCode}.", request.OriginId, request.WorkflowId, errorCode);
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

        public void Dispose() => _httpClient.Dispose();
    }
}
