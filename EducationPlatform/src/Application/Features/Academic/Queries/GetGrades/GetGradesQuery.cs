using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Domain.AcademicManagement.Aggregate;
using AutoMapper;

namespace Application.Features.Academic.Queries.GetGrades
{
    public class GetGradesQuery : IRequest<IEnumerable<GradeDTO>>
    {
        public bool IncludeInactive { get; set; } = false;
    }

    public class GetGradesQueryHandler : IRequestHandler<GetGradesQuery, IEnumerable<GradeDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetGradesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GradeDTO>> Handle(GetGradesQuery request, CancellationToken cancellationToken)
        {
            var list = await _unitOfWork
                .GetRepository<IGradeRepository>()
                .GetAllAsync(cancellationToken);

            if (!request.IncludeInactive)
            {
                list = list.Where(g => g.IsActive).ToList();
            }

            // An empty catalogue is an answer, not an error: the client shows an empty dropdown
            if (list == null || !list.Any())
                return new List<GradeDTO>();

            return _mapper.Map<IEnumerable<GradeDTO>>(list);
        }
    }
}
