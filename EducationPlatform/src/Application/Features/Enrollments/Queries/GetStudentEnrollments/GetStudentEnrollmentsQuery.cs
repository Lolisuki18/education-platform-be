using Application.Results;
using MediatR;
using Infrastructure.Interface;
using AutoMapper;

namespace Application.Features.Enrollments.Queries.GetStudentEnrollments
{
    public class GetStudentEnrollmentsQuery : IRequest<IEnumerable<EnrollmentDTO>>
    {
        public Guid StudentID { get; set; }
    }

    public class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, IEnumerable<EnrollmentDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetStudentEnrollmentsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<EnrollmentDTO>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
        {
            var list = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetStudentEnrollments(request.StudentID);

            return _mapper.Map<IEnumerable<EnrollmentDTO>>(list);
        }
    }
}
