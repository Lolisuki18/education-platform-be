using Infrastructure.DataAccessException;
using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class GenericRepository<T> : IGenericRepository<T>
        where T : class
    {
        protected readonly EducationPlatformDBContext context;

        public GenericRepository(EducationPlatformDBContext context)
        {
            this.context = context;
        }

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

        public void Update(Guid id, T entity)
        {
            var existingEntity = context.Set<T>().Find(id);
            if (existingEntity == null)
                throw new RepositoryException($"Entity with ID:{id} is not found");

            context.Entry(existingEntity).CurrentValues.SetValues(entity);
        }

        public void Delete(Guid id)
        {
            var entity = context.Set<T>().Find(id);
            if (entity != null)
                context.Set<T>().Remove(entity);
        }
    }
}
