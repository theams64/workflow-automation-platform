using Backend.Api.Services.Common;
using Backend.Api.Services.WorkflowExecution;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Time;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Api.Controllers
{

    [ApiController]
    [Route("test")]
    public sealed class TestController : ControllerBase
    {
        private readonly IWorkflowExecutionService _workflowExecutionService;
        private readonly IClock _clock;
        private readonly IWebHostEnvironment _environment;

        public TestController(IWorkflowExecutionService workflowExecutionService, IClock clock, IWebHostEnvironment environment)
        {
            _workflowExecutionService = workflowExecutionService;
            _clock = clock;
            _environment = environment;
        }

        [HttpGet("protected")]
        public IActionResult ProtectedEndpoint() => Ok(new { message = "You are authenticated." });

        [HttpPost("workflow/{workflowId:int}/run-scheduled")]
        public async Task<ActionResult<WorkflowRunResult>> RunScheduledWorkflow(int workflowId, [FromBody] JsonElement workflowInputs, [FromQuery] DateTimeOffset? scheduledFor, CancellationToken cancellationToken)
        {
            // Keep this temporary endpoint unavailable outside Development,
            // even if the controller itself is accidentally deployed.
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            // A real scheduler would know the scheduled occurrence time and pass
            // that value to the execution service. For an ad-hoc development run,
            // use the current application clock unless a specific occurrence is
            // supplied explicitly through the query string.
            var simulatedScheduledFor = scheduledFor ?? _clock.UtcNow;

            var result = await _workflowExecutionService.ExecuteAsync(workflowId, WorkflowTriggerTypes.Schedule, workflowInputs, scheduledFor: simulatedScheduledFor, cancellationToken: cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "workflow.not_found"))
                {
                    return NotFound(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        private static bool HasError(IReadOnlyList<ServiceError> errors, string code)
        {
            return errors.Any(error => error.Code == code);
        }
    }
}
