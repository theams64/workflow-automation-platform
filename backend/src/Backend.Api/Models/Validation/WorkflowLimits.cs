using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Backend.Api.Models.Validation
{
    public static class WorkflowLimits
    {
        // Workflow limits
        public const int NameMaxLength = 200;
        public const int TriggerTypeMaxLength = 50;
        public const int CronExpressionMaxLength = 100;
        public const int TimezoneMaxLength = 100;

        public const int StepKeyMaxLength = 64;
        public const int StepTypeMaxLength = 100;
        public const int ConfigJsonMaxLength = 32 * 1024;
        public const int MaxStepsPerWorkflow = 50;

        public const int ExecutionStatusMaxLength = 32;
        public const int ExecutionErrorCodeMaxLength = 100;
        public const int ExecutionErrorMessageMaxLength = 500;

        public const int RuntimeInputMaxBytes = 32 * 1024;
        public const int NormalizedOutputMaxBytes = 256 * 1024;

        public const int DefaultStepTimeoutSeconds = 30;
        public const int MaximumStepTimeoutSeconds = 120;
        public const int DefaultWorkflowTimeoutSeconds = 300;
        public const int MaximumWorkflowTimeoutSeconds = 900;

        // Http Level 1 limits
        public const int HttpOriginIdMaxLength = 64;
        public const int HttpPathMaxLength = 1024;
        public const int HttpPathSegmentMaxLength = 256;
        public const int HttpMaximumUrlLength = 8192;
        public const int HttpMaximumQueryParameters = 100;
        public const int HttpMaximumQueryNameLength = 128;
        public const int HttpMaximumQueryValueLength = 4096;
        public const int HttpMaximumResponseHeaderBytes = 64 * 1024;
        public const int HttpMaximumCompressedResponseBytes = 1024 * 1024;
        public const int HttpMaximumDecompressedResponseBytes = 64 * 1024;
        public const int HttpMaximumJsonDepth = 64;
        public const int HttpMaximumJsonTokenCount = 100_000;
        public const int HttpMaximumJsonPropertiesPerObject = 10_000;
        public const int HttpMaximumJsonArrayItems = 10_000;
        public const int HttpMaximumJsonStringBytes = 64 * 1024;
        public const int HttpMaximumTimeoutMilliseconds = 120_000;
        public const int HttpMaximumConcurrencyLimit = 1024;
        public const int HttpMaximumApprovedOrigins = 100;
        public const int HttpMaximumPathPrefixes = 64;
        public const int HttpMaximumSelectedResponseHeaders = 20;

        // Shared expression limits
        public const int ExpressionMaximumDepth = 16;
        public const int ExpressionMaximumOperations = 10_000;
        public const int ExpressionMaximumTemplateBytes = 32 * 1024;
        public const int ExpressionMaximumStringBytes = 64 * 1024;
        public const int ExpressionMaximumFunctionArguments = 8;

        // Transform limits
        public const int TransformMaximumDepth = 32;
        public const int TransformMaximumCollectionItems = 1_000;
        public const int TransformMaximumPropertiesPerObject = 2_000;
        public const int TransformMaximumStaticArrayItems = 1_000;

        // Composition limits
        public const int CompositionMaximumItems = 100;
        public const int CompositionMaximumMessageBytes = 32 * 1024;
        public const int CompositionMaximumSeparatorBytes = 256;

        // Managed connection limits
        public const int ConnectionNameMaxLength = 200;
        public const int ConnectionTypeMaxLength = 64;
        public const int ConnectionCanonicalOriginMaxLength = 256;
        public const int ConnectionCredentialTypeMaxLength = 64;
        public const int ConnectionSecretReferenceMaxLength = 128;
        public const int ConnectionCredentialPlacementMaxLength = 32;

        // Slack limits
        public const int SlackMaximumMessageBytes = 32 * 1024;
        public const int SlackMaximumResponseBytes = 4 * 1024;

        // Page limits
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
    }
}