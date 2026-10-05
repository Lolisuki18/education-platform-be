namespace API.Helpers
{
    /// <summary>
    /// Marks the endpoints a client must still be able to call with an access token whose role is out of date:
    /// the ones that exist to renew it (refresh-token) or to end the session (logout). Everything else makes the
    /// client refresh first, see <see cref="UserActiveMiddleware"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AllowStaleRoleAttribute : Attribute
    {
    }
}
