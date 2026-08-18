using Backend.Api.Configuration;

namespace Backend.Api.WorkflowEngine.Http
{
    public static class SafeHttpMessageHandlerFactory
    {
        public static SocketsHttpHandler Create(SafeHttpOptions options, IPublicNetworkConnector connector)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(connector);

            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                UseCookies = false,
                UseProxy = false,
                Proxy = null,
                Credentials = null,
                PreAuthenticate = false,
                ActivityHeadersPropagator = null,
                ConnectTimeout = Timeout.InfiniteTimeSpan,
                ConnectCallback = connector.ConnectAsync,
                MaxConnectionsPerServer = options.PerOriginConcurrencyLimit,
                MaxResponseHeadersLength = Math.Max(1, (options.MaxResponseHeaderBytes + 1023) / 1024),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
            };
        }
    }
}
