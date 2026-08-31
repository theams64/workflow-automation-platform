using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.ManagedConnection
{
    public sealed class ManagedConnectionListRequestDto
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, WorkflowLimits.MaxPageSize)]
        public int PageSize { get; set; } = WorkflowLimits.DefaultPageSize;
    }
}