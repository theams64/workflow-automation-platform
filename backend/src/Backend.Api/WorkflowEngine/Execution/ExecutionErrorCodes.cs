namespace Backend.Api.WorkflowEngine.Execution
{
    public static class ExecutionErrorCodes
    {
        // Workflow error codes
        public const string InvalidWorkflow = "invalid_workflow";
        public const string InvalidStepConfiguration = "invalid_step_configuration";
        public const string InvalidReference = "invalid_reference";
        public const string UnknownStepType = "unknown_step_type";
        public const string StepFailed = "step_failed";
        public const string StepTimeout = "step_timeout";
        public const string WorkflowTimeout = "workflow_timeout";
        public const string WorkflowCancelled = "workflow_cancelled";
        public const string OutputTooLarge = "output_too_large";

        // Http error codes
        public const string InvalidOrigin = "invalid_origin";
        public const string OriginDisabled = "origin_disabled";
        public const string MethodNotAllowed = "method_not_allowed";
        public const string PathNotAllowed = "path_not_allowed";
        public const string QueryParameterNotAllowed = "query_parameter_not_allowed";
        public const string RequestUriInvalid = "request_uri_invalid";
        public const string ConnectionTimeout = "connection_timeout";
        public const string RequestTimeout = "request_timeout";
        public const string NetworkDestinationNotAllowed = "network_destination_not_allowed";
        public const string RedirectNotAllowed = "redirect_not_allowed";
        public const string ResponseHeadersTooLarge = "response_headers_too_large";
        public const string ResponseTooLarge = "response_too_large";
        public const string InvalidResponse = "invalid_response";
        public const string HttpRequestFailed = "http_request_failed";
        public const string HttpNonSuccessStatus = "http_non_success_status";
        public const string ConcurrencyLimitExceeded = "concurrency_limit_exceeded";

        // Transform / expression error codes
        public const string TransformValidationFailed = "transform_validation_failed";
        public const string TransformExecutionFailed = "transform_execution_failed";
        public const string TemplateValidationFailed = "template_validation_failed";
        public const string MessageSizeExceeded = "message_size_exceeded";

        // Managed connection / Slack error codes
        public const string InvalidConnection = "invalid_connection";
        public const string ConnectionDisabled = "connection_disabled";
        public const string ConnectionRevoked = "connection_revoked";
        public const string ConnectionSecretUnavailable = "connection_secret_unavailable";
        public const string SlackDeliveryFailed = "slack_delivery_failed";
    }
}
