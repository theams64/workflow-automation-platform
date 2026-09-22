using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Backend.Api.Tests.Common
{
    public static class SafeHttpTestHelpers
    {
        public static SafeHttpOptions Options() => new();

        public static OutboundRequestPolicy Policy(
            string originId = "test-origin",
            bool enabled = true,
            IReadOnlyList<string>? pathPrefixes = null,
            IReadOnlySet<string>? queryParameters = null,
            IReadOnlySet<string>? selectedHeaders = null,
            int maximumResponseBytes = 64 * 1024,
            int headerTimeoutMilliseconds = 1_000,
            int bodyTimeoutMilliseconds = 1_000) =>
            new(
                originId,
                new Uri("https://api.example.com", UriKind.Absolute),
                enabled,
                new HashSet<string>(["GET"], StringComparer.Ordinal),
                pathPrefixes ?? ["/forecast"],
                queryParameters ?? new HashSet<string>(["q"], StringComparer.Ordinal),
                selectedHeaders ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                maximumResponseBytes,
                headerTimeoutMilliseconds,
                bodyTimeoutMilliseconds
            );

        public static SafeHttpRequest Request(
            string path = "/forecast", 
            IReadOnlyDictionary<string, string?>? query = null, 
            HttpMethod? method = null, 
            int maximumResponseBytes = 64 * 1024) =>
            new(
                UserId: 7,
                WorkflowId: 11,
                OriginId: "test-origin",
                Method: method ?? HttpMethod.Get,
                Path: path,
                Query: query ?? new Dictionary<string, string?>(StringComparer.Ordinal),
                MaximumResponseBytes: maximumResponseBytes
            );

        public static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        public static HttpResponseMessage GzipJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var input = Encoding.UTF8.GetBytes(json);

            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(input);
            }

            var content = new ByteArrayContent(buffer.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("gzip");

            return new HttpResponseMessage(statusCode)
            {
                Content = content
            };
        }

        public static HttpResponseMessage BrotliJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var input = Encoding.UTF8.GetBytes(json);

            using var buffer = new MemoryStream();

            using (var brotli = new BrotliStream(buffer, CompressionMode.Compress, leaveOpen: true))
            {
                brotli.Write(input);
            }

            var content = new ByteArrayContent(buffer.ToArray());

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add("br");

            return new HttpResponseMessage(statusCode)
            {
                Content = content
            };
        }

        public static HttpResponseMessage EncodedResponse(string encoding, byte[] body, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var content = new ByteArrayContent(body);

            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Headers.ContentEncoding.Add(encoding);

            return new HttpResponseMessage(statusCode)
            {
                Content = content
            };
        }

        public static SafeOutboundHttpClient CreateClient(
            HttpMessageHandler handler,
            SafeHttpOptions? options = null,
            IOutboundConcurrencyLimiter? limiter = null,
            ILogger<SafeOutboundHttpClient>? logger = null) =>
            new(
                handler,
                Microsoft.Extensions.Options.Options.Create(options ?? Options()),
                limiter ?? new NoopOutboundConcurrencyLimiter(),
                logger ?? NullLogger<SafeOutboundHttpClient>.Instance
            );
    }

    public sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    public sealed class NoopOutboundConcurrencyLimiter : IOutboundConcurrencyLimiter
    {
        public ValueTask<IAsyncDisposable> AcquireAsync(SafeHttpRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<IAsyncDisposable>(new Lease());

        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    public sealed class SlowReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
