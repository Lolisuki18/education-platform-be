using AutoMapper;
using API.Models.Courses;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Features.Courses.ReviewCourse;
using API.Models.Statistics;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Features.Statistics.Queries.GetTopPerformance;

namespace API.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ListCoursesRequestDto, GetLandingPageQuery>();
            CreateMap<ReviewCourseRequestDto, ReviewCourseCommand>();

            CreateMap<SummaryStatisticsRequestDto, GetSummaryStatisticsQuery>()
                .ForMember(d => d.From, opt => opt.MapFrom(s => s.Query.From))
                .ForMember(d => d.To, opt => opt.MapFrom(s => s.Query.To));

            CreateMap<AnalyticsGrowthRequestDto, GetAnalyticsGrowthQuery>()
                .ForMember(d => d.Type, opt => opt.MapFrom(s => s.Query.Type))
                .ForMember(d => d.From, opt => opt.MapFrom(s => s.Query.From))
                .ForMember(d => d.To, opt => opt.MapFrom(s => s.Query.To))
                .ForMember(d => d.GroupBy, opt => opt.MapFrom(s => s.Query.GroupBy))
                .ForMember(d => d.UserRole, opt => opt.MapFrom(s => s.Query.UserRole))
                .ForMember(d => d.CourseGradeId, opt => opt.MapFrom(s => s.Query.CourseGradeId))
                .ForMember(d => d.CourseSubjectId, opt => opt.MapFrom(s => s.Query.CourseSubjectId))
                .ForMember(d => d.EnrollmentGradeId, opt => opt.MapFrom(s => s.Query.EnrollmentGradeId))
                .ForMember(d => d.EnrollmentSubjectId, opt => opt.MapFrom(s => s.Query.EnrollmentSubjectId))
                .ForMember(d => d.RevenueType, opt => opt.MapFrom(s => s.Query.RevenueType))
                .ForMember(d => d.ComparisonRanges, opt => opt.MapFrom(s => s.Query.ComparisonRanges));

            CreateMap<AnalyticsGrowthRequestDto, GetAnalyticsDemandAndSupplyQuery>()
                .ForMember(d => d.From, opt => opt.MapFrom(s => s.Query.From))
                .ForMember(d => d.To, opt => opt.MapFrom(s => s.Query.To))
                .ForMember(d => d.GroupBy, opt => opt.MapFrom(s => s.Query.GroupBy))
                .ForMember(d => d.CourseGradeId, opt => opt.MapFrom(s => s.Query.CourseGradeId))
                .ForMember(d => d.CourseSubjectId, opt => opt.MapFrom(s => s.Query.CourseSubjectId))
                .ForMember(d => d.EnrollmentGradeId, opt => opt.MapFrom(s => s.Query.EnrollmentGradeId))
                .ForMember(d => d.EnrollmentSubjectId, opt => opt.MapFrom(s => s.Query.EnrollmentSubjectId));

            CreateMap<AnalyticsGrowthRequestDto, GetAnalyticsNormalizedGrowthQuery>()
                .ForMember(d => d.Type, opt => opt.MapFrom(s => s.Query.Type))
                .ForMember(d => d.From, opt => opt.MapFrom(s => s.Query.From))
                .ForMember(d => d.To, opt => opt.MapFrom(s => s.Query.To))
                .ForMember(d => d.GroupBy, opt => opt.MapFrom(s => s.Query.GroupBy))
                .ForMember(d => d.UserRole, opt => opt.MapFrom(s => s.Query.UserRole))
                .ForMember(d => d.CourseGradeId, opt => opt.MapFrom(s => s.Query.CourseGradeId))
                .ForMember(d => d.CourseSubjectId, opt => opt.MapFrom(s => s.Query.CourseSubjectId))
                .ForMember(d => d.EnrollmentGradeId, opt => opt.MapFrom(s => s.Query.EnrollmentGradeId))
                .ForMember(d => d.EnrollmentSubjectId, opt => opt.MapFrom(s => s.Query.EnrollmentSubjectId))
                .ForMember(d => d.RevenueType, opt => opt.MapFrom(s => s.Query.RevenueType));

            CreateMap<TopPerformanceRequestDto, GetTopPerformanceQuery>()
                .ForMember(d => d.From, opt => opt.MapFrom(s => s.Query.From))
                .ForMember(d => d.To, opt => opt.MapFrom(s => s.Query.To))
                .ForMember(d => d.GradeId, opt => opt.MapFrom(s => s.Query.GradeId))
                .ForMember(d => d.SubjectId, opt => opt.MapFrom(s => s.Query.SubjectId))
                .ForMember(d => d.Top, opt => opt.MapFrom(s => s.Query.Top));
        }
    }
}
