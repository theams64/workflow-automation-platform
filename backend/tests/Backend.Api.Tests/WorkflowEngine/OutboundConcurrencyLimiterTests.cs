using Backend.Api.Configuration;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class OutboundConcurrencyLimiterTests
    {
        [Fact]
        public async Task AcquireAsync_ShouldFailFastWhenLimitIsOccupied()
        {
            var options = Options.Create(new SafeHttpOptions
            {
                GlobalConcurrencyLimit = 1,
                PerUserConcurrencyLimit = 1,
                PerWorkflowConcurrencyLimit = 1,
                PerOriginConcurrencyLimit = 1
            });

            var sut = new OutboundConcurrencyLimiter(options);
            var request = SafeHttpTestHelpers.Request();

            await using var first = await sut.AcquireAsync(request, CancellationToken.None);

            var action = async () =>
            {
                await using var _ = await sut.AcquireAsync(request, CancellationToken.None);
            };

            var exception = await action.Should().ThrowAsync<SafeHttpException>();
            exception.Which.Code.Should().Be(ExecutionErrorCodes.ConcurrencyLimitExceeded);
        }

        [Fact]
        public async Task AcquireAsync_ShouldReleaseAllGatesWithLease()
        {
            var options = Options.Create(new SafeHttpOptions
            {
                GlobalConcurrencyLimit = 1,
                PerUserConcurrencyLimit = 1,
                PerWorkflowConcurrencyLimit = 1,
                PerOriginConcurrencyLimit = 1
            });

            var sut = new OutboundConcurrencyLimiter(options);
            var request = SafeHttpTestHelpers.Request();

            await using (var first = await sut.AcquireAsync(request, CancellationToken.None))
            {
            }

            await using var second = await sut.AcquireAsync(request, CancellationToken.None);

            second.Should().NotBeNull();
        }
    }
}