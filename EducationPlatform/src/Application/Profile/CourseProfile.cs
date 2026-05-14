using AutoMapper;
using Application.Results;
using Domain.CourseManagement.Aggregate;

namespace Application.Mappings
{
    public class CourseProfile : AutoMapper.Profile
    {
        public CourseProfile()
        {
            // Map từ Domain → DTO
            CreateMap<Course, CourseDTO>();
        }
    }
}
