using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using MediatR;

namespace Application.Features.Courses.Queries.GetPolicies
{
    public class GetPoliciesQueryHandler : IRequestHandler<GetPoliciesQuery, IEnumerable<PolicyDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetPoliciesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PolicyDTO>> Handle(GetPoliciesQuery request, CancellationToken cancellationToken)
        {
            var policies = await _unitOfWork
                .GetRepository<IPolicyRepository>()
                .GetAllAsync();

            if (request.ActiveOnly)
            {
                policies = policies.Where(p => p.IsActive);
            }

            return _mapper.Map<IEnumerable<PolicyDTO>>(policies);
        }
    }
}
