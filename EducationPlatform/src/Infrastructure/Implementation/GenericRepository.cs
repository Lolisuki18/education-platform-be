using Infrastructure.DataAccessException;
using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await context.Set<T>().FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await context.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
            return entities ?? Enumerable.Empty<T>();
        }

        public void Add(T entity)
        {
            context.Set<T>().Add(entity);
        }

        public async Task UpdateAsync(Guid id, T entity, CancellationToken cancellationToken = default)
        {
            var existingEntity = await context.Set<T>().FindAsync(new object[] { id }, cancellationToken);
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
