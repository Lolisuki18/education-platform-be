using Application.Features.Courses.Queries.GetPolicies;
using Application.Results;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Courses.Queries.GetPolicies
{
    public class GetPoliciesQueryHandler : IRequestHandler<GetPoliciesQuery, IEnumerable<PolicyDTO>>
    {
        private readonly EducationPlatformDBContext _context;
        private readonly IMapper _mapper;

        public GetPoliciesQueryHandler(EducationPlatformDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<PolicyDTO>> Handle(GetPoliciesQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Policies
                .AsNoTracking();

            if (request.ActiveOnly)
            {
                query = query.Where(p => p.IsActive);
            }

            var policies = await query
                .ProjectTo<PolicyDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return policies;
        }
    }
}
