using AutoMapper;
using API.Models.Courses;
using Application.Features.Courses.ReviewCourse;

namespace API.Helpers
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<ReviewCourseRequestDto, ReviewCourseCommand>();

            // Without this map a review that names violated chapters failed with a 500 (AutoMapper had no rule for the items)
            CreateMap<ViolatedChapterRequestItem, ViolatedChapterItem>()
                .ForMember(d => d.AdminNote, opt => opt.MapFrom(s => s.AdminNote ?? string.Empty));
        }
    }
}
