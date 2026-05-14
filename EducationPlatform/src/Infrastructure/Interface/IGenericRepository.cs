namespace Infrastructure.Interface
{
    public interface IGenericRepository<T>
        where T : class
    {
        Task<T?> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        void Add(T entity);
        T Update(Guid id, T entity);
        void Remove(Guid id);
    }
}

