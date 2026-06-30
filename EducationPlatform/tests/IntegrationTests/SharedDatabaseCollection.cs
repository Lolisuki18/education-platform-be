using Xunit;

namespace IntegrationTests
{
    [CollectionDefinition("Shared database collection")]
    public class SharedDatabaseCollection : ICollectionFixture<CustomWebApplicationFactory>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
