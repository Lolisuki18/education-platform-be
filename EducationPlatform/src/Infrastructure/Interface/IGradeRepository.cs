using Domain.AcademicManagement.Aggregate;

namespace Infrastructure.Interface
{
    public interface IGradeRepository : 
        IGenericRepository<Grade>,
        IRepositoryBase
    {
    }
}

