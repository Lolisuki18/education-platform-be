namespace Domain.Common.Interfaces
{
    public interface IUnitOfWork : System.IAsyncDisposable, System.IDisposable
    {
        T GetRepository<T>() where T : class;

        Task BeginTransactionAsync();
        Task<int> CommitAsync(string? performedBy = null);
    }
}
