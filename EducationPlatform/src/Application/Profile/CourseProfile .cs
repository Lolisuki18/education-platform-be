using AutoMapper;
using Application.Results;          // nơi chứa CourseDTO
using Domain.CourseManagement.Aggregate;

public class CourseProfile : Profile
{
    public CourseProfile()
    {
        // Map từ Domain → DTO
        CreateMap<Course, CourseDTO>();
        CreateMap<API.Models.Courses.ListCoursesRequestDto, Application.Features.Courses.Queries.GetLandingPage.GetLandingPageQuery>();
        CreateMap<API.Models.Courses.ReviewCourseRequestDto, Application.Features.Courses.ReviewCourse.ReviewCourseCommand>();
    }
}
