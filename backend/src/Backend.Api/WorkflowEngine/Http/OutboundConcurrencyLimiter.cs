using Backend.Api.Configuration;
using Backend.Api.WorkflowEngine.Execution;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Backend.Api.WorkflowEngine.Http
{
    public sealed class OutboundConcurrencyLimiter : IOutboundConcurrencyLimiter
    {
        private readonly SemaphoreSlim _global;
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _users = new();
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _workflows = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _origins = new(StringComparer.Ordinal);

        private readonly int _perUser;
        private readonly int _perWorkflow;
        private readonly int _perOrigin;

        public OutboundConcurrencyLimiter(IOptions<SafeHttpOptions> options)
        {
            var value = options.Value;
            _global = new SemaphoreSlim(value.GlobalConcurrencyLimit, value.GlobalConcurrencyLimit);
            _perUser = value.PerUserConcurrencyLimit;
            _perWorkflow = value.PerWorkflowConcurrencyLimit;
            _perOrigin = value.PerOriginConcurrencyLimit;
        }

        public async ValueTask<IAsyncDisposable> AcquireAsync(SafeHttpRequest request, CancellationToken cancellationToken)
        {
            var gates = new[]
            {
                _global,
                _users.GetOrAdd(request.UserId, _ => new SemaphoreSlim(_perUser, _perUser)),
                _workflows.GetOrAdd(request.WorkflowId, _ => new SemaphoreSlim(_perWorkflow, _perWorkflow)),
                _origins.GetOrAdd(request.OriginId, _ => new SemaphoreSlim(_perOrigin, _perOrigin))
            };

            var acquired = new List<SemaphoreSlim>(gates.Length);

            try
            {
                foreach (var gate in gates)
                {
                    if (!await gate.WaitAsync(0, cancellationToken))
                    {
                        throw new SafeHttpException(ExecutionErrorCodes.ConcurrencyLimitExceeded, "The outbound HTTP concurrency limit was reached.");
                    }

                    acquired.Add(gate);
                }

                return new Lease(acquired);
            }
            catch
            {
                for (var index = acquired.Count - 1; index >= 0; index--)
                {
                    acquired[index].Release();
                }

                throw;
            }
        }

        private sealed class Lease(IReadOnlyList<SemaphoreSlim> gates) : IAsyncDisposable
        {
            private int _disposed;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                {
                    return ValueTask.CompletedTask;
                }

                for (var index = gates.Count - 1; index >= 0; index--)
                {
                    gates[index].Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
