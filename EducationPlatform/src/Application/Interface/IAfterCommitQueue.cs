namespace Application.Interface
{
    /// <summary>
    /// Work that must only happen once the current unit of work has been committed, e.g. sending an e-mail about
    /// a payment. Domain event handlers run before the save, so side effects triggered there belong here.
    /// </summary>
    public interface IAfterCommitQueue
    {
        void Enqueue(Func<Task> action);
    }
}
