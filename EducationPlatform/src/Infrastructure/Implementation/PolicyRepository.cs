using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class PolicyRepository :
        GenericRepository<PolicyRule>,
        IPolicyRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public PolicyRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        #endregion
    }
}

