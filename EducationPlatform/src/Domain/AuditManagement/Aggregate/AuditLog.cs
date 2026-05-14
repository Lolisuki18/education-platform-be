namespace Domain.AuditManagement.Aggregate
{
    public class AuditLog
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid AuditLogId { get; private set; } = Guid.NewGuid();
        public string EntityName { get; private set; } = string.Empty;
        public string Action { get; private set; } = string.Empty;
        public string? PerformedBy { get; private set; }
        public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
        public string? OldValue { get; private set; }
        public string? NewValue { get; private set; }
        #endregion

        protected AuditLog() { }

        public AuditLog(
            string entityName,
            string action,
            string? performedBy,
            string? oldValue,
            string? newValue)
        {
            EntityName = entityName;
            Action = action;
            PerformedBy = performedBy;
            OldValue = oldValue;
            NewValue = newValue;
            Timestamp = DateTime.UtcNow;
        }

        #region Methods
        #endregion
    }
}

