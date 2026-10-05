namespace Infrastructure.Persistence.Seeds
{
    public class SeedOptions
    {
        /// <summary>
        /// Fake teachers, students, courses and orders for local development and demos. Never enable this on a
        /// real system: the accounts share one well-known password and the orders distort revenue statistics.
        /// </summary>
        public bool DemoData { get; set; }

        /// <summary>Password of the demo accounts (<c>SEED_DEFAULT_PASSWORD</c> overrides it).</summary>
        public string DemoPassword { get; set; } = DefaultDemoPassword;

        /// <summary>The first administrator of a real system. Created only when no admin exists yet.</summary>
        public AdminSeedOptions Admin { get; set; } = new();

        public const string DefaultDemoPassword = "Demo@18102004";
    }

    public class AdminSeedOptions
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string Phone { get; set; } = "0000000000";
        public string Name { get; set; } = "Platform Administrator";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
    }
}
