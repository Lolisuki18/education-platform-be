namespace Application.Interface
{
    /// <summary>
    /// Short-lived cache of "is this account still active", consulted on every authenticated request.
    /// Changing an account's status must call <see cref="Invalidate"/> so the change applies immediately.
    /// </summary>
    public interface IUserActivityCache
    {
        bool TryGetIsActive(Guid userId, out bool isActive);

        void SetIsActive(Guid userId, bool isActive);

        void Invalidate(Guid userId);
    }
}
