using Application.Results;
using MediatR;

namespace Application.Features.Courses.Queries.GetPolicies
{
    /// <summary>
    /// Query: Fetch all active policies with their rules.
    /// Policies are semi-static reference data (change rarely) —
    /// making this a great candidate for caching in a future phase.
    /// </summary>
    public class GetPoliciesQuery : IRequest<IEnumerable<PolicyDTO>>
    {
        /// <summary>
        /// When true, returns only active policies.
        /// Default: true — Admin review UI only needs active policies.
        /// </summary>
        public bool ActiveOnly { get; set; } = true;
    }
}
