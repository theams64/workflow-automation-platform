using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed class PublicNetworkConnector(IOptions<SafeHttpOptions> options) : IPublicNetworkConnector
    {
        public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.Value.ConnectTimeoutMilliseconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.Unspecified, linked.Token);

                var allowed = addresses
                    .Where(PublicNetworkAddressPolicy.IsAllowed)
                    .Distinct()
                    .ToArray();

                if (allowed.Length == 0)
                {
                    throw new SafeHttpException(ExecutionErrorCodes.NetworkDestinationNotAllowed, "The approved HTTP origin resolved to a disallowed network destination.");
                }

                foreach (var address in allowed)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true
                    };

                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), linked.Token);

                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (OperationCanceledException)
                    {
                        socket.Dispose();
                        throw;
                    }
                    catch (SocketException)
                    {
                        socket.Dispose();
                    }
                }

                throw new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP connection could not be established.");
            }
            catch (SafeHttpException)
            {
                throw;
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new SafeHttpException(ExecutionErrorCodes.ConnectionTimeout, "The outbound HTTP connection timed out.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SocketException)
            {
                throw new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP connection could not be established.");
            }
        }
    }
}
