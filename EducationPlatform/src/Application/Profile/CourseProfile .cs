using AutoMapper;
using Application.Results;          // nơi chứa CourseDTO
using Domain.CourseManagement.Aggregate;

public class CourseProfile : Profile
{
    public CourseProfile()
    {
        // Map từ Domain → DTO
        CreateMap<Course, CourseDTO>();
    }
}
