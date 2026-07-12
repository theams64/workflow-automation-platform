using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class RefreshTokenOptions
    {
        public const string SectionName = "RefreshTokens";

        [Range(1, 30)]
        public int LifetimeDays { get; init; } = 14;

        [Range(1, 20)]
        public int MaximumActiveSessionsPerUser { get; init; } = 5;

        [Range(32, 128)]
        public int TokenSizeBytes { get; init; } = 64;
    }
}
