using Application.Interface;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// Collects actions during a request and runs them when the unit of work reports a successful commit.
    /// A failing action is logged and never undoes the commit that already happened.
    /// </summary>
    public class AfterCommitQueue : IAfterCommitQueue
    {
        private readonly List<Func<Task>> _actions = new();
        private readonly ILogger<AfterCommitQueue> _logger;

        public AfterCommitQueue(ILogger<AfterCommitQueue> logger)
        {
            _logger = logger;
        }

        public void Enqueue(Func<Task> action)
        {
            _actions.Add(action);
        }

        /// <summary>Drops everything: the transaction these actions belonged to was rolled back.</summary>
        public void Clear()
        {
            _actions.Clear();
        }

        public async Task RunAsync()
        {
            if (_actions.Count == 0)
                return;

            // Copy first: an action may enqueue more work or trigger another commit
            var pending = _actions.ToList();
            _actions.Clear();

            foreach (var action in pending)
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An action scheduled for after the commit failed.");
                }
            }
        }
    }
}
