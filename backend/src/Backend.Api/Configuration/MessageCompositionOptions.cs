using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class MessageCompositionOptions
    {
        public const string SectionName = "MessageComposition";

        [Range(1, WorkflowLimits.CompositionMaximumItems)]
        public int MaxItems { get; init; } = 25;

        [Range(128, WorkflowLimits.CompositionMaximumMessageBytes)]
        public int MaxMessageBytes { get; init; } = 16 * 1024;

        [Range(1, WorkflowLimits.CompositionMaximumSeparatorBytes)]
        public int MaxSeparatorBytes { get; init; } = 64;
    }
}