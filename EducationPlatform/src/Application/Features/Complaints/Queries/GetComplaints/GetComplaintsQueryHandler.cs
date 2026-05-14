using Application.Features.Complaints.Queries.GetComplaints;
using Application.Results;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Complaints.Queries.GetComplaints
{
    public class GetComplaintsQueryHandler : IRequestHandler<GetComplaintsQuery, IEnumerable<ComplaintDTO>>
    {
        private readonly EducationPlatformDBContext _context;
        private readonly IMapper _mapper;

        public GetComplaintsQueryHandler(EducationPlatformDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ComplaintDTO>> Handle(GetComplaintsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Complaints
                .AsNoTracking();

            if (request.Status.HasValue)
            {
                query = query.Where(c => c.Status == request.Status.Value);
            }

            if (request.TeacherId.HasValue)
            {
                query = query.Where(c => c.Course.TeacherID == request.TeacherId.Value);
            }

            var complaints = await query
                .OrderByDescending(c => c.CreatedAt)
                .ProjectTo<ComplaintDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return complaints;
        }
    }
}
