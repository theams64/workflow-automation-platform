using Backend.Api.Models.Dtos.ManagedConnection;
using Backend.Api.Services.ManagedConnection;
using Backend.Api.Services.Workflow;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("connection")]
    public sealed class ManagedConnectionController : ControllerBase
    {
        private readonly IManagedConnectionService _connection;

        public ManagedConnectionController(IManagedConnectionService connection)
        {
            _connection = connection;
        }

        [HttpPost]
        public async Task<ActionResult<ManagedConnectionResponseDto>> Create([FromBody] CreateManagedConnectionRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _connection.CreateAsync(request, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "connection.duplicate_name"))
                {
                    return Conflict(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return CreatedAtAction(nameof(GetById), new { connectionId = result.Data!.Id }, result.Data);
        }

        [HttpGet]
        public async Task<ActionResult<ManagedConnectionListResponseDto>> GetAll([FromQuery] ManagedConnectionListRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _connection.GetAllAsync(request, cancellationToken);

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

        [HttpGet("{connectionId:guid}")]
        public async Task<ActionResult<ManagedConnectionResponseDto>> GetById(Guid connectionId, CancellationToken cancellationToken)
        {
            var result = await _connection.GetByIdAsync(connectionId, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "connection.not_found"))
                {
                    return NotFound(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [HttpPut("{connectionId:guid}")]
        public async Task<ActionResult<ManagedConnectionResponseDto>> Update(Guid connectionId, [FromBody] UpdateManagedConnectionRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _connection.UpdateAsync(connectionId, request, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "connection.not_found"))
                {
                    return NotFound(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "connection.duplicate_name"))
                {
                    return Conflict(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        [HttpPost("{connectionId:guid}/revoke")]
        public async Task<ActionResult<ManagedConnectionResponseDto>> Revoke(Guid connectionId, CancellationToken cancellationToken)
        {
            var result = await _connection.RevokeAsync(connectionId, cancellationToken);

            if (!result.Succeeded)
            {
                if (HasError(result.Errors, "auth.unauthorized"))
                {
                    return Unauthorized(new { errors = result.Errors });
                }

                if (HasError(result.Errors, "connection.not_found"))
                {
                    return NotFound(new { errors = result.Errors });
                }

                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result.Data);
        }

        private static bool HasError(IReadOnlyList<Services.Common.ServiceError> errors, string code)
        {
            return errors.Any(error => error.Code == code);
        }
    }
}