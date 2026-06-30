using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Academic;
using API.Helper;
using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Academic.Commands.CreateGrade;
using Application.Features.Academic.Commands.UpdateGrade;
using Application.Features.Academic.Commands.DeactivateGrade;
using Application.Features.Academic.Commands.CreateSubject;
using Application.Features.Academic.Commands.UpdateSubject;
using Application.Features.Academic.Commands.DeactivateSubject;
using Application.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/academic")]
    public class AcademicController : ControllerBase
    {
        private readonly IMediator mediator;

        public AcademicController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet("grades")]
        public async Task<ActionResult<ApiResponse<IEnumerable<GradeDTO>>>> GetGrades([FromQuery] bool includeInactive = false)
        {
            if (includeInactive && !User.IsInRole("Admin"))
            {
                return StatusCode(403, ApiResponse.Success("Forbidden: Only Admin can view inactive grades.", 403));
            }

            var result = await mediator.Send(new GetGradesQuery { IncludeInactive = includeInactive });
            return Ok(ApiResponse<IEnumerable<GradeDTO>>.Success(result));
        }

        [HttpGet("subjects")]
        public async Task<ActionResult<ApiResponse<IEnumerable<SubjectDTO>>>> GetSubjects([FromQuery] bool includeInactive = false)
        {
            if (includeInactive && !User.IsInRole("Admin"))
            {
                return StatusCode(403, ApiResponse.Success("Forbidden: Only Admin can view inactive subjects.", 403));
            }

            var result = await mediator.Send(new GetSubjectsQuery { IncludeInactive = includeInactive });
            return Ok(ApiResponse<IEnumerable<SubjectDTO>>.Success(result));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPost("grades")]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateGrade([FromBody] CreateGradeRequestDto request)
        {
            var command = new CreateGradeCommand { Name = request.Name };
            var result = await mediator.Send(command);
            return Ok(ApiResponse<Guid>.Success(result, "Grade created successfully."));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPut("grades/{id:guid}")]
        public async Task<ActionResult<ApiResponse>> UpdateGrade(Guid id, [FromBody] UpdateGradeRequestDto request)
        {
            var command = new UpdateGradeCommand
            {
                GradeID = id,
                Name = request.Name,
                IsActive = request.IsActive
            };
            await mediator.Send(command);
            return Ok(ApiResponse.Success("Grade updated successfully."));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpDelete("grades/{id:guid}")]
        public async Task<ActionResult<ApiResponse>> DeactivateGrade(Guid id)
        {
            await mediator.Send(new DeactivateGradeCommand { GradeID = id });
            return Ok(ApiResponse.Success("Grade deactivated successfully."));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPost("subjects")]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateSubject([FromBody] CreateSubjectRequestDto request)
        {
            var command = new CreateSubjectCommand { Code = request.Code, Name = request.Name };
            var result = await mediator.Send(command);
            return Ok(ApiResponse<Guid>.Success(result, "Subject created successfully."));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPut("subjects/{id:guid}")]
        public async Task<ActionResult<ApiResponse>> UpdateSubject(Guid id, [FromBody] UpdateSubjectRequestDto request)
        {
            var command = new UpdateSubjectCommand
            {
                SubjectID = id,
                Code = request.Code,
                Name = request.Name,
                IsActive = request.IsActive
            };
            await mediator.Send(command);
            return Ok(ApiResponse.Success("Subject updated successfully."));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpDelete("subjects/{id:guid}")]
        public async Task<ActionResult<ApiResponse>> DeactivateSubject(Guid id)
        {
            await mediator.Send(new DeactivateSubjectCommand { SubjectID = id });
            return Ok(ApiResponse.Success("Subject deactivated successfully."));
        }
    }
}
