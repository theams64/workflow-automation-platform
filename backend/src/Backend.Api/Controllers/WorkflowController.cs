using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Services.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("workflow")]
    public sealed class WorkflowController : ControllerBase
    {
        private readonly IWorkflowService _workflow;

        public WorkflowController(IWorkflowService workflow)
        {
            _workflow = workflow;
        }

        [HttpPost]
        public async Task<ActionResult<WorkflowResponseDto>> CreateWorkflow([FromBody] CreateWorkflowRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _workflow.CreateWorkflowAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "workflow.duplicate_name"))
                {
                    return Conflict(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                return BadRequest(new
                {
                    errors = result.Errors
                });
            }

            return CreatedAtAction(nameof(GetWorkflowById), new { workflowId = result.Data!.Id }, result.Data);
        }

        [HttpGet]
        public async Task<ActionResult<WorkflowListResponseDto>> GetAllWorkflows([FromQuery] WorkflowListRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _workflow.GetWorkflowsAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [HttpGet("{workflowId:int}")]
        public async Task<ActionResult<WorkflowDetailResponseDto>> GetWorkflowById(int workflowId, CancellationToken cancellationToken)
        {
            var result = await _workflow.GetWorkflowByIdAsync(workflowId, cancellationToken);

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

        [HttpPut("{workflowId:int}")]
        public async Task<ActionResult<WorkflowResponseDto>> UpdateWorkflow(int workflowId, [FromBody] UpdateWorkflowRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _workflow.UpdateWorkflowAsync(workflowId, request, cancellationToken);

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

                if (HasError(result.Errors, "workflow.duplicate_name"))
                {
                    return Conflict(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [HttpDelete("{workflowId:int}")]
        public async Task<ActionResult> DeleteWorkflow(int workflowId, CancellationToken cancellationToken)
        {
            var result = await _workflow.DeleteWorkflowAsync(workflowId, cancellationToken);

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

            return NoContent();
        }

        private static bool HasError(IReadOnlyList<Services.Common.ServiceError> errors, string code)
        {
            return errors.Any(error => error.Code == code);
        }
    }
}
