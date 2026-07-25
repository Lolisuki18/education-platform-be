using Application.Results;
using MediatR;

namespace Application.Features.Courses.Queries.GetPolicies
{
    public class GetPoliciesQuery : IRequest<IEnumerable<PolicyDTO>>
    {
        public bool ActiveOnly { get; set; } = true;
    }
}
