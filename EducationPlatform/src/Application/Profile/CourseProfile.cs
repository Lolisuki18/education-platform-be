using Application.Results;
using AutoMapper;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.IdentityManagement.Aggregate;

namespace Application.Mappings
{
    public class CourseProfile : AutoMapper.Profile
    {
        public CourseProfile()
        {
            // Map from Domain to DTO
            CreateMap<Course, CourseDTO>();
            CreateMap<CourseReview, CourseReviewDTO>()
                .ForMember(dest => dest.CourseReviewID, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.CourseName, opt => opt.MapFrom(src => src.Course.Title))
                .ForMember(dest => dest.Reviewer, opt => opt.MapFrom(src => src.Student.Name));
        }
    }
}
