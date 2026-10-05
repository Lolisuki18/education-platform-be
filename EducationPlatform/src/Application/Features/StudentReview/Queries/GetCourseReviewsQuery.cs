using Application.Exceptions;
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
        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Application.Common.Paging.NormalizePageIndex(value);
        }
        private int _pageSize = Application.Common.Paging.DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Application.Common.Paging.NormalizePageSize(value);
        }
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
            var courseExist = await _unitOfWork.GetRepository<ICourseRepository>().GetByIdAsync(request.CourseId, cancellationToken);

            if (courseExist == null) throw new NotFoundException($"Course not found.");

            //2. get list of reviews for that course
            var reviews = await _unitOfWork.GetRepository<ICourseReviewRepository>()
                .GetReviewsByCourseId(request.CourseId, request.PageIndex, request.PageSize, cancellationToken);
            return _mapper.Map<IEnumerable<CourseReviewDTO>>(reviews);
        }
    }
}
