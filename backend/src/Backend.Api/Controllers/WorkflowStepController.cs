using Backend.Api.Models.Dtos.WorkflowStep;
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
        public async Task<ActionResult<WorkflowStepListResponseDto>> GetWorkflowSteps(int workflowId, CancellationToken ct)
        {
            var result = await _workflow.GetWorkflowStepsAsync(workflowId, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpPost]
        public async Task<ActionResult<WorkflowStepListResponseDto>> CreateWorkflowSteps(int workflowId, [FromBody] SaveWorkflowStepsRequestDto dto, CancellationToken ct)
        {
            var result = await _workflow.CreateWorkflowStepsAsync(workflowId, dto, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                var hasConflict = result.Errors.Any(e => e.Code == "workflow_steps.already_exist");
                if (hasConflict)
                {
                    return base.Conflict(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpPut]
        public async Task<ActionResult<WorkflowStepResponseDto>> ReplaceWorkflowSteps(int workflowId, [FromBody] SaveWorkflowStepsRequestDto dto, CancellationToken ct)
        {
            var result = await _workflow.ReplaceWorkflowStepsAsync(workflowId, dto, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteWorkflowSteps(int workflowId, CancellationToken ct)
        {
            var result = await _workflow.DeleteWorkflowStepsAsync(workflowId, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.NoContent();
        }
    }
}
