using AutoMapper;
using API.Models.Courses;
using Application.Features.Courses.ReviewCourse;

namespace API.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ReviewCourseRequestDto, ReviewCourseCommand>();
        }
    }
}
