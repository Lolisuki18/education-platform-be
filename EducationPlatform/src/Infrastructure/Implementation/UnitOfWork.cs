using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.AuditManagement.Aggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Infrastructure.Implementation
{
    public class UnitOfWork : IUnitOfWork
    {
        #region Attributes
        private readonly IServiceProvider provider;
        private readonly Dictionary<Type, object> repositories = new();

        private readonly EducationPlatformDBContext context;
        private IDbContextTransaction? transaction;
        #endregion

        #region Properties
        #endregion

        public UnitOfWork(
            EducationPlatformDBContext context,
            IServiceProvider provider)
        {
            this.context = context;
            this.provider = provider;
        }

        #region Methods
        public T GetRepository<T>() where T : class
        {
            var type = typeof(T);

            if (!repositories.TryGetValue(type, out var repo))
            {
                // Resolve from DI
                repo = provider.GetRequiredService(type);

                // Cache it
                repositories[type] = repo;
            }

            return (T)repo;
        }

        public async Task BeginTransactionAsync()
        {
            if (transaction == null)
            {
                transaction = await context.Database.BeginTransactionAsync();
            }
        }

        public async Task<int> CommitAsync(
            string? performedBy = null)
        {
            try
            {
                await AddAuditLogsAsync(performedBy);

                int changed = await context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return changed;
            }
            catch
            {
                await RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }
            }
        }

        private async Task RollbackAsync()
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
                await transaction.DisposeAsync();
                transaction = null;
            }
        }
        #endregion

        #region Audit Logging
        private async Task AddAuditLogsAsync(string? performedBy)
        {
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added ||
                            e.State == EntityState.Modified ||
                            e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                string entityName = entry.Entity.GetType().Name;
                string action = entry.State.ToString();

                string? oldValue = entry.State == EntityState.Added
                    ? null
                    : JsonSerializer.Serialize(entry.OriginalValues.ToObject());

                string? newValue = entry.State == EntityState.Deleted
                    ? null
                    : JsonSerializer.Serialize(entry.CurrentValues.ToObject());

                var auditLog = new AuditLog(
                    entityName: entityName,
                    action: action,
                    performedBy: performedBy,
                    oldValue: oldValue,
                    newValue: newValue
                );

                GetRepository<IAuditRepository>().Add(auditLog);
            }
        }
        #endregion

        public void Dispose()
        {
            // Gọi DisposeAsync một cách đồng bộ để tương thích với IDisposable
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (transaction != null)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Nuốt lỗi nếu connection hoặc transaction đã bị đóng băng trước đó
                }
                finally
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }
            }
        }
    }
}

