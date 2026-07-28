using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Services.Common;
using Backend.Api.Services.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("workflow/{workflowId:int}/step")]
    public sealed class WorkflowStepController : ControllerBase
    {
        private readonly IWorkflowService _workflow;

        public WorkflowStepController(IWorkflowService workflow)
        {
            _workflow = workflow;
        }

        [HttpGet]
        public async Task<ActionResult<WorkflowStepListResponseDto>> GetWorkflowSteps(int workflowId, CancellationToken cancellationToken)
        {
            var result = await _workflow.GetWorkflowStepsAsync(workflowId, cancellationToken);

            return MapStepResult(result);
        }

        [HttpPost]
        public async Task<ActionResult<WorkflowStepListResponseDto>> CreateWorkflowSteps(int workflowId, [FromBody] SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _workflow.CreateWorkflowStepsAsync(workflowId, request, cancellationToken);

            return MapStepResult(result);
        }

        [HttpPut]
        public async Task<ActionResult<WorkflowStepListResponseDto>> ReplaceWorkflowSteps(int workflowId, [FromBody] SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _workflow.ReplaceWorkflowStepsAsync(workflowId, request, cancellationToken);

            return MapStepResult(result);
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteWorkflowSteps(int workflowId, CancellationToken cancellationToken)
        {
            var result = await _workflow.DeleteWorkflowStepsAsync(workflowId, cancellationToken);

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

        private ActionResult<WorkflowStepListResponseDto> MapStepResult(ServiceResult<WorkflowStepListResponseDto> result)
        {
            if (result.Succeeded)
            {
                return Ok(result.Data);
            }

            if (HasError(result.Errors, "auth.unauthorized"))
            {
                return Unauthorized(new { errors = result.Errors });
            }

            if (HasError(result.Errors, "workflow.not_found"))
            {
                return NotFound(new { errors = result.Errors });
            }

            if (HasError(result.Errors, "workflow_steps.already_exist") || HasError(result.Errors, "workflow_steps.conflict"))
            {
                return Conflict(new { errors = result.Errors });
            }

            return BadRequest(new { errors = result.Errors });
        }

        private static bool HasError(IReadOnlyList<ServiceError> errors, string code)
        {
            return errors.Any(error => error.Code == code);
        }
    }
}
