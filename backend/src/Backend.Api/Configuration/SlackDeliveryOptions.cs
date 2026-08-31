using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class SlackDeliveryOptions
    {
        public const string SectionName = "SlackDelivery";

        [Range(128, WorkflowLimits.SlackMaximumMessageBytes)]
        public int MaxMessageBytes { get; init; } = 16 * 1024;

        [Range(1, WorkflowLimits.SlackMaximumResponseBytes)]
        public int MaxResponseBytes { get; init; } = 1024;

        [Range(10, WorkflowLimits.HttpMaximumTimeoutMilliseconds)]
        public int ResponseHeadersTimeoutMilliseconds { get; init; } = 10_000;

        [Range(10, WorkflowLimits.HttpMaximumTimeoutMilliseconds)]
        public int BodyReadTimeoutMilliseconds { get; init; } = 10_000;
    }
}