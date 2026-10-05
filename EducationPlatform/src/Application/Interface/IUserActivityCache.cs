namespace Application.Interface
{
    /// <summary>What the API needs to know about an account on every authenticated request.</summary>
    public readonly record struct UserStatus(bool IsActive, string Role);

    /// <summary>
    /// Short-lived cache of <see cref="UserStatus"/>, consulted on every authenticated request.
    /// Changing an account's status or role must call <see cref="Invalidate"/> so the change applies immediately.
    /// </summary>
    public interface IUserActivityCache
    {
        bool TryGet(Guid userId, out UserStatus status);

        void Set(Guid userId, UserStatus status);

        void Invalidate(Guid userId);
    }
}
