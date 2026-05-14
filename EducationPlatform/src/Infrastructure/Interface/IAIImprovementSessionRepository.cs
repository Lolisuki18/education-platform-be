using Domain.AIManagement.Aggregate;

namespace Infrastructure.Interface
{
    internal interface IAIImprovementSessionRepository :
        IGenericRepository<AIImprovementSession>,
        IRepositoryBase
    {
    }
}

