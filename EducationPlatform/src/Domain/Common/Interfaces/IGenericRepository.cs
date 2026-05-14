namespace Domain.Common.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(Guid id);
        void Add(T entity);
        void Update(Guid id, T entity);
        void Delete(Guid id);
    }
}
