using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Services;
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
        private readonly AfterCommitQueue afterCommit;
        private IDbContextTransaction? transaction;
        #endregion

        #region Properties
        #endregion

        public UnitOfWork(
            EducationPlatformDBContext context,
            IServiceProvider provider,
            AfterCommitQueue afterCommit)
        {
            this.context = context;
            this.provider = provider;
            this.afterCommit = afterCommit;
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
            int changed;

            try
            {
                await AddAuditLogsAsync(performedBy);

                changed = await context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                await RollbackAsync();

                // Whatever was waiting for this commit (e-mails, ...) must not happen now
                afterCommit.Clear();
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

            await afterCommit.RunAsync();

            return changed;
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
        /// <summary>
        /// Entities that never belong in the audit trail: credentials (the owned Password type is tracked as its
        /// own entity, so a name filter on properties alone would write password hashes here), session
        /// tokens, and high-volume records that are not business changes.
        /// </summary>
        private static readonly HashSet<string> UnauditedEntities = new(StringComparer.Ordinal)
        {
            "Password",
            "RefreshSession",
            "Notification",
            "AuditLog"
        };

        /// <summary>
        /// Personal data of a user is never copied into the audit trail: it would outlive the account and could
        /// not be erased with it. The trail still records which user changed and when.
        /// </summary>
        private static readonly HashSet<string> UserPersonalProperties = new(StringComparer.Ordinal)
        {
            "Email",
            "Phone",
            "Name",
            "Bio"
        };

        private async Task AddAuditLogsAsync(string? performedBy)
        {
            var entries = context.ChangeTracker.Entries()
                .Where(e => (e.State == EntityState.Added ||
                             e.State == EntityState.Modified ||
                             e.State == EntityState.Deleted) &&
                            !UnauditedEntities.Contains(e.Entity.GetType().Name))
                .ToList();

            foreach (var entry in entries)
            {
                string entityName = entry.Entity.GetType().Name;
                string action = entry.State.ToString();

                var originalValuesDict = new Dictionary<string, object?>();
                var currentValuesDict = new Dictionary<string, object?>();

                foreach (var prop in entry.Properties)
                {
                    var propName = prop.Metadata.Name;

                    // Filter out sensitive properties (passwords, refresh tokens, OTPs)
                    if (propName.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase) ||
                        propName.Contains("Otp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (entityName == "User" && UserPersonalProperties.Contains(propName))
                        continue;

                    originalValuesDict[propName] = prop.OriginalValue;
                    currentValuesDict[propName] = prop.CurrentValue;
                }

                string? oldValue = entry.State == EntityState.Added
                    ? null
                    : JsonSerializer.Serialize(originalValuesDict);

                string? newValue = entry.State == EntityState.Deleted
                    ? null
                    : JsonSerializer.Serialize(currentValuesDict);

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
            if (transaction != null)
            {
                transaction.Dispose();
                transaction = null;
            }
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

