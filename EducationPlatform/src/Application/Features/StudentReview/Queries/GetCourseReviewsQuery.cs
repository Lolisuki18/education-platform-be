using Application.BusinessException;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using MediatR;


namespace Application.Features.StudentReview.Queries
{
    public class GetCourseReviewsQuery : IRequest<IEnumerable<CourseReviewDTO>>
    {
        public Guid CourseId { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetCourseReviewsQueryHandler : IRequestHandler<GetCourseReviewsQuery, IEnumerable<CourseReviewDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetCourseReviewsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CourseReviewDTO>> Handle(GetCourseReviewsQuery request, CancellationToken cancellationToken)
        {
            //1.Check is course exist
            var courseExist = await _unitOfWork.GetRepository<ICourseRepository>().GetByIdAsync(request.CourseId);

            if (courseExist == null) throw new NotFound($"Course not found.");

            //2. get list of reviews for that course
            var reviews = await _unitOfWork.GetRepository<ICourseReviewRepository>()
                .GetReviewsByCourseId(request.CourseId, request.PageIndex, request.PageSize);
            return _mapper.Map<IEnumerable<CourseReviewDTO>>(reviews);
        }
    }
}
