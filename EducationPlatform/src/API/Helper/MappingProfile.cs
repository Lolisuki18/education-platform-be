using AutoMapper;
using API.Models.Courses;
using Application.Features.Courses.ReviewCourse;

namespace API.Helper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Redundant mappings for Courses and Statistics have been removed.
            // Mediating Commands/Queries are now used directly as DTOs.

            CreateMap<ReviewCourseRequestDto, ReviewCourseCommand>();
        }
    }
}
