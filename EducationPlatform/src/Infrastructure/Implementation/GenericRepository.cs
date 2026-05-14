using Infrastructure.DataAccessException;
using Infrastructure.Interface;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class GenericRepository<T> : IGenericRepository<T>
        where T : class
    {
        #region Attributes
        protected readonly EducationPlatformDBContext context;
        #endregion

        #region Properties
        #endregion

        public GenericRepository(EducationPlatformDBContext context)
        {
            this.context = context;
        }

        #region Methods
        public async Task<T?> GetByIdAsync(Guid id)
        {
            return await context.Set<T>().FindAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var entities = await context.Set<T>().AsNoTracking().ToListAsync();
            return entities ?? Enumerable.Empty<T>();
        }

        public void Add(T entity)
        {
            context.Set<T>().Add(entity);
        }

        public T Update(Guid id, T entity)
        {
            var existingEntity = context.Set<T>().Find(id);
            if (existingEntity == null)
                throw new RepositoryException($"Entity with ID:{id} is not found");

            context.Entry(existingEntity).CurrentValues.SetValues(entity);
            return existingEntity;
        }

        public void Remove(Guid id)
        {
            var entity = context.Set<T>().Find(id);
            if (entity != null)
                context.Set<T>().Remove(entity);
        }
        #endregion
    }
}

