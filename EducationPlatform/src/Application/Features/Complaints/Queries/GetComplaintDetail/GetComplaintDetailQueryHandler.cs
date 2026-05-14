using Application.BusinessException;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Results;
using AutoMapper;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Complaints.Queries.GetComplaintDetail
{
    public class GetComplaintDetailQueryHandler : IRequestHandler<GetComplaintDetailQuery, ComplaintDetailDTO>
    {
        private readonly EducationPlatformDBContext _context;
        private readonly IMapper _mapper;

        public GetComplaintDetailQueryHandler(EducationPlatformDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<ComplaintDetailDTO> Handle(GetComplaintDetailQuery request, CancellationToken cancellationToken)
        {
            var complaint = await _context.Complaints
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.Course)
                    .ThenInclude(c => c.Teacher)
                .Include(c => c.Course)
                    .ThenInclude(c => c.Grade)
                .Include(c => c.Course)
                    .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(c => c.ComplaintID == request.ComplaintID, cancellationToken);

            if (complaint == null)
            {
                throw new NotFound($"Complaint with ID: {request.ComplaintID} is not found");
            }

            return _mapper.Map<ComplaintDetailDTO>(complaint);
        }
    }
}
