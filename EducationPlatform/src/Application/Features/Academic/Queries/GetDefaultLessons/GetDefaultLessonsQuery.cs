using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Domain.AcademicManagement.Aggregate;
using AutoMapper;

namespace Application.Features.Academic.Queries.GetDefaultLessons
{
    public class GetDefaultLessonsQuery : IRequest<IEnumerable<DefaultLessonDTO>>
    {
        public Guid SubjectId { get; set; }
        public Guid GradeId { get; set; }
    }

    public class GetDefaultLessonsQueryHandler : IRequestHandler<GetDefaultLessonsQuery, IEnumerable<DefaultLessonDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetDefaultLessonsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<DefaultLessonDTO>> Handle(GetDefaultLessonsQuery request, CancellationToken cancellationToken)
        {
            var list = await _unitOfWork
                .GetRepository<ISubjectRepository>()
                .GetDefaultLessons(request.SubjectId, request.GradeId, cancellationToken);

            // A subject/grade without a template is normal: the teacher then writes the course from scratch
            if (list == null || !list.Any())
                return new List<DefaultLessonDTO>();

            return _mapper.Map<IEnumerable<DefaultLessonDTO>>(list);
        }
    }
}
