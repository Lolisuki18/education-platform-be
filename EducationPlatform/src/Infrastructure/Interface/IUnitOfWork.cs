namespace Infrastructure.Interface
{
    public interface IUnitOfWork
    {
        T GetRepository<T>() where T : class;

        Task BeginTransactionAsync();
        Task<int> CommitAsync(string? performedBy = null);
    }

    public interface IRepositoryBase
    {

    }
}

