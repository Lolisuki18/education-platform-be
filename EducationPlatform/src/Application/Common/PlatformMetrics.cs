using System.Diagnostics.Metrics;

namespace Application.Common
{
    /// <summary>
    /// Business counters worth alerting on. They cost nothing until a listener is attached; with
    /// <c>OpenTelemetry:OtlpEndpoint</c> configured they are exported next to the HTTP and runtime metrics.
    /// </summary>
    public static class PlatformMetrics
    {
        public const string MeterName = "EducationPlatform";

        private static readonly Meter Meter = new(MeterName, "1.0");

        /// <summary>Orders created, tagged <c>kind</c> = paid | free.</summary>
        public static readonly Counter<long> OrdersCreated =
            Meter.CreateCounter<long>("education.orders.created", description: "Orders created");

        /// <summary>Orders that reached the paid state (webhook, redirect or a free order).</summary>
        public static readonly Counter<long> OrdersPaid =
            Meter.CreateCounter<long>("education.orders.paid", description: "Orders paid");

        /// <summary>Unpaid orders cancelled after their payment link expired.</summary>
        public static readonly Counter<long> OrdersCancelled =
            Meter.CreateCounter<long>("education.orders.cancelled", description: "Unpaid orders cancelled");

        /// <summary>PayOS refused or failed to create a payment link: students could not pay.</summary>
        public static readonly Counter<long> PaymentLinkFailures =
            Meter.CreateCounter<long>("education.payments.link_failures", description: "Payment links that could not be created");

        /// <summary>Webhooks received from PayOS, tagged <c>result</c> = Processed | Ignored | InvalidSignature | Malformed.</summary>
        public static readonly Counter<long> PaymentWebhooks =
            Meter.CreateCounter<long>("education.payments.webhooks", description: "PayOS webhooks received");

        /// <summary>Accounts locked after too many failed logins.</summary>
        public static readonly Counter<long> AccountLockouts =
            Meter.CreateCounter<long>("education.auth.lockouts", description: "Accounts locked after failed logins");

        /// <summary>A rotated refresh token was presented again (possible token theft); every session of the user was revoked.</summary>
        public static readonly Counter<long> RefreshTokenReplays =
            Meter.CreateCounter<long>("education.auth.refresh_token_replays", description: "Replayed refresh tokens detected");

        /// <summary>
        /// Security-relevant events, tagged <c>event</c> = login_failed | login_locked_out | password_reset_requested |
        /// password_reset_completed | password_changed | account_deleted | ... (see SecurityEventBehavior).
        /// </summary>
        public static readonly Counter<long> SecurityEvents =
            Meter.CreateCounter<long>("education.security.events", description: "Security-relevant events");

        /// <summary>E-mails handed to SMTP, tagged <c>result</c> = sent | failed (failed = gave up after the retries).</summary>
        public static readonly Counter<long> EmailsSent =
            Meter.CreateCounter<long>("education.emails", description: "E-mails processed by the background sender");
    }
}
