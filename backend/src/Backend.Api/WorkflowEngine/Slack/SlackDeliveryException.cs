namespace Backend.Api.WorkflowEngine.Slack
{
    public sealed class SlackDeliveryException : Exception
    {
        public SlackDeliveryException(string code, string safeMessage) : base(safeMessage)
        {
            Code = code;
            SafeMessage = safeMessage;
        }

        public string Code { get; }
        public string SafeMessage { get; }
    }
}