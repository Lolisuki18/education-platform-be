using API.Models.Statistics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Features.Statistics.Queries.GetSummaryStatistic;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Features.Statistics.Queries.GetTopPerformance;
using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;

namespace API.Controllers
{
    [ApiController]
    [Route("api/statistics")]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : ControllerBase
    {
        private readonly IMediator mediator;

        public StatisticsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<SummaryStatisticsResponseDto>> GetSummary([FromQuery] SummaryStatisticsRequestDto request)
        {
            request.Query.From ??= DateTime.Now.AddMonths(-1);
            request.Query.To ??= DateTime.Now;

            var grades = await mediator.Send(new GetGradesQuery());
            var subjects = await mediator.Send(new GetSubjectsQuery());
            
            var summary = await mediator.Send(new GetSummaryStatisticQuery
            {
                From = request.Query.From,
                To = request.Query.To
            });

            return Ok(new SummaryStatisticsResponseDto
            {
                Grades = grades,
                Subjects = subjects,
                Summary = summary
            });
        }

        [HttpGet("analytics/growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await mediator.Send(new GetAnalyticsGrowthQuery
            {
                Type = request.Query.Type,
                From = request.Query.From,
                To = request.Query.To,
                GroupBy = request.Query.GroupBy,
                UserRole = request.Query.UserRole,
                CourseGradeId = request.Query.CourseGradeId,
                CourseSubjectId = request.Query.CourseSubjectId,
                EnrollmentGradeId = request.Query.EnrollmentGradeId,
                EnrollmentSubjectId = request.Query.EnrollmentSubjectId,
                RevenueType = request.Query.RevenueType,
                ComparisonRanges = request.Query.ComparisonRanges
            });
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/demand-supply")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsDemandAndSupply([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await mediator.Send(new GetAnalyticsDemandAndSupplyQuery
            {
                From = request.Query.From,
                To = request.Query.To,
                GroupBy = request.Query.GroupBy,
                CourseGradeId = request.Query.CourseGradeId,
                CourseSubjectId = request.Query.CourseSubjectId,
                EnrollmentGradeId = request.Query.EnrollmentGradeId,
                EnrollmentSubjectId = request.Query.EnrollmentSubjectId
            });
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/normalized-growth")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetAnalyticsNormalizedGrowth([FromQuery] AnalyticsGrowthRequestDto request)
        {
            var data = await mediator.Send(new GetAnalyticsNormalizedGrowthQuery
            {
                Type = request.Query.Type,
                From = request.Query.From,
                To = request.Query.To,
                GroupBy = request.Query.GroupBy,
                UserRole = request.Query.UserRole,
                CourseGradeId = request.Query.CourseGradeId,
                CourseSubjectId = request.Query.CourseSubjectId,
                EnrollmentGradeId = request.Query.EnrollmentGradeId,
                EnrollmentSubjectId = request.Query.EnrollmentSubjectId,
                RevenueType = request.Query.RevenueType
            });
            return Ok(new AnalyticsResponseDto { Data = data });
        }

        [HttpGet("analytics/top-performance")]
        public async Task<ActionResult<AnalyticsResponseDto>> GetTopPerformance([FromQuery] TopPerformanceRequestDto request)
        {
            var data = await mediator.Send(new GetTopPerformanceQuery
            {
                From = request.Query.From,
                To = request.Query.To,
                GradeId = request.Query.GradeId,
                SubjectId = request.Query.SubjectId,
                Top = request.Query.Top
            });
            return Ok(new AnalyticsResponseDto { Data = data });
        }
    }
}
