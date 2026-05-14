using Infrastructure.Interface;
using Infrastructure.Persistence;
using Domain.AIManagement.Aggregate;

namespace Infrastructure.Implementation
{
    public class AIImprovementSessionRepository :
        GenericRepository<AIImprovementSession>,
        IAIImprovementSessionRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public AIImprovementSessionRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        #endregion
    }
}

