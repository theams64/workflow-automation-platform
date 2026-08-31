using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class TransformOptions
    {
        public const string SectionName = "Transform";

        [Range(1, WorkflowLimits.TransformMaximumDepth)]
        public int MaxOutputDepth { get; init; } = 16;

        [Range(1, WorkflowLimits.TransformMaximumCollectionItems)]
        public int MaxCollectionItems { get; init; } = 100;

        [Range(1, WorkflowLimits.TransformMaximumPropertiesPerObject)]
        public int MaxPropertiesPerObject { get; init; } = 256;

        [Range(1, WorkflowLimits.TransformMaximumStaticArrayItems)]
        public int MaxStaticArrayItems { get; init; } = 100;
    }
}