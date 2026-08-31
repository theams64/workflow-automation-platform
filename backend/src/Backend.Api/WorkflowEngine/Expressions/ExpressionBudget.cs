using Backend.Api.WorkflowEngine.Execution;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Expressions
{
    public sealed class ExpressionBudget
    {
        private readonly int _maximumOperations;
        private readonly CancellationToken _cancellationToken;
        private int _operations;

        public ExpressionBudget(int maximumOperations, CancellationToken cancellationToken)
        {
            if (maximumOperations <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumOperations));
            }

            _maximumOperations = maximumOperations;
            _cancellationToken = cancellationToken;
        }

        public int Operations => _operations;

        public void Consume(int amount = 1)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            _operations = checked(_operations + amount);

            if (_operations > _maximumOperations)
            {
                throw new WorkflowExpressionException("expression_operation_limit", "The workflow expression exceeded the allowed operation count.");
            }
        }
    }
}