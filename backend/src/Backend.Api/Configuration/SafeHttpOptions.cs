using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed record class SafeHttpOptions
    {
        public const string SectionName = "SafeHttp";

        [Range(256, WorkflowLimits.HttpMaximumUrlLength)]
        public int MaxUrlLength { get; init; } = 2048;

        [Range(1, WorkflowLimits.HttpMaximumQueryParameters)]
        public int MaxQueryParameters { get; init; } = 20;

        [Range(1, WorkflowLimits.HttpMaximumQueryNameLength)]
        public int MaxQueryNameLength { get; init; } = 64;

        [Range(1, WorkflowLimits.HttpMaximumQueryValueLength)]
        public int MaxQueryValueLength { get; init; } = 512;

        [Range(1, WorkflowLimits.HttpMaximumResponseHeaderBytes)]
        public int MaxResponseHeaderBytes { get; init; } = 16 * 1024;

        [Range(1, WorkflowLimits.HttpMaximumCompressedResponseBytes)]
        public int MaxCompressedResponseBytes { get; init; } = 128 * 1024;

        [Range(1, WorkflowLimits.HttpMaximumDecompressedResponseBytes)]
        public int MaxDecompressedResponseBytes { get; init; } = 64 * 1024;

        [Range(1, WorkflowLimits.HttpMaximumJsonDepth)]
        public int MaxJsonDepth { get; init; } = 32;

        [Range(1, WorkflowLimits.HttpMaximumJsonTokenCount)]
        public int MaxJsonTokenCount { get; init; } = 20_000;

        [Range(1, WorkflowLimits.HttpMaximumJsonPropertiesPerObject)]
        public int MaxJsonPropertiesPerObject { get; init; } = 2_000;

        [Range(1, WorkflowLimits.HttpMaximumJsonArrayItems)]
        public int MaxJsonArrayItems { get; init; } = 2_000;

        [Range(1, WorkflowLimits.HttpMaximumJsonStringBytes)]
        public int MaxJsonStringBytes { get; init; } = 16 * 1024;

        [Range(10, WorkflowLimits.HttpMaximumTimeoutMilliseconds)]
        public int ConnectTimeoutMilliseconds { get; init; } = 5_000;

        [Range(10, WorkflowLimits.HttpMaximumTimeoutMilliseconds)]
        public int ResponseHeadersTimeoutMilliseconds { get; init; } = 10_000;

        [Range(10, WorkflowLimits.HttpMaximumTimeoutMilliseconds)]
        public int BodyReadTimeoutMilliseconds { get; init; } = 20_000;

        [Range(1, WorkflowLimits.HttpMaximumConcurrencyLimit)]
        public int GlobalConcurrencyLimit { get; init; } = 32;

        [Range(1, WorkflowLimits.HttpMaximumConcurrencyLimit)]
        public int PerUserConcurrencyLimit { get; init; } = 4;

        [Range(1, WorkflowLimits.HttpMaximumConcurrencyLimit)]
        public int PerWorkflowConcurrencyLimit { get; init; } = 2;

        [Range(1, WorkflowLimits.HttpMaximumConcurrencyLimit)]
        public int PerOriginConcurrencyLimit { get; init; } = 8;
    }
}
