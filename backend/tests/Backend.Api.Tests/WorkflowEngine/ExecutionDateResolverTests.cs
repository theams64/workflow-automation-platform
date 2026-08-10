using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Time;
using FluentAssertions;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class ExecutionDateResolverTests
    {
        // January 1, 2026 at 3:00 AM UTC
        // = December 31, 2025 at 9:00 PM in America/Chicago (CST, UTC-6)
        private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero));

        [Fact]
        public void Resolve_ShouldPreferExplicitDate()
        {
            var sut = new ExecutionDateResolver(_clock, new TimezoneValidator());

            var result = sut.Resolve(new("America/Chicago", new DateOnly(2026, 1, 1), new DateTimeOffset(2026, 1, 3, 18, 0, 0, TimeSpan.Zero)));

            result.Should().Be(new DateOnly(2026, 1, 1));
        }

        [Fact]
        public void Resolve_ShouldUseScheduledDateInWorkflowTimezone()
        {
            var sut = new ExecutionDateResolver(_clock, new TimezoneValidator());

            // January 1, 2026 at 2:00 AM UTC
            // = December 31, 2025 at 8:00 PM in America/Chicago
            var result = sut.Resolve(new("America/Chicago", null, new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.Zero)));

            result.Should().Be(new DateOnly(2025, 12, 31));
        }

        [Fact]
        public void Resolve_ShouldUseCurrentDateInWorkflowTimezone()
        {
            var sut = new ExecutionDateResolver(_clock, new TimezoneValidator());

            var result = sut.Resolve(new("America/Chicago", null, null));

            result.Should().Be(new DateOnly(2025, 12, 31));
        }
    }
}
