using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.CourseManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class PolicyRepository :
        GenericRepository<Policy>,
        IPolicyRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public PolicyRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<IEnumerable<Policy>> GetDetailPolicies()
        {
            return await context.Policies
                .AsNoTracking()
                .Include(p => p.PolicyRules)
                .ToListAsync();
        }
        #endregion
    }
}

