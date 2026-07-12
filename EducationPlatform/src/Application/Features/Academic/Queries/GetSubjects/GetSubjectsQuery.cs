using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Domain.AcademicManagement.Aggregate;
using AutoMapper;

namespace Application.Features.Academic.Queries.GetSubjects
{
    public class GetSubjectsQuery : IRequest<IEnumerable<SubjectDTO>>
    {
        public bool IncludeInactive { get; set; } = false;
    }

    public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, IEnumerable<SubjectDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetSubjectsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<SubjectDTO>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
        {
            var list = await _unitOfWork
                .GetRepository<ISubjectRepository>()
                .GetAllAsync();

            if (!request.IncludeInactive)
            {
                list = list.Where(s => s.IsActive).ToList();
            }

            if (list == null || !list.Any())
                throw new NotFound("Subject list is empty or was not found");

            return _mapper.Map<IEnumerable<SubjectDTO>>(list);
        }
    }
}
