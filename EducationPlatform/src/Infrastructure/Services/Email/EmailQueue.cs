using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Email
{
    public record EmailMessage(string To, string Subject, string Body);

    /// <summary>
    /// In-memory queue between the request that wants an e-mail sent and the background sender.
    /// Messages still waiting when the process dies are lost, which is acceptable for OTPs and receipts
    /// (the user can ask again); use a persistent broker if that ever stops being true.
    /// </summary>
    public class EmailQueue
    {
        private const int Capacity = 1000;

        private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true
            });

        private readonly ILogger<EmailQueue> _logger;

        public EmailQueue(ILogger<EmailQueue> logger)
        {
            _logger = logger;
        }

        public ChannelReader<EmailMessage> Reader => _channel.Reader;

        /// <summary>Never blocks the caller: a full queue drops the message and says so in the log.</summary>
        public bool Enqueue(EmailMessage message)
        {
            if (_channel.Writer.TryWrite(message))
                return true;

            _logger.LogError("E-mail queue is full or closed; dropped a message for {To}.", message.To);
            return false;
        }

        public void Complete() => _channel.Writer.TryComplete();
    }
}
