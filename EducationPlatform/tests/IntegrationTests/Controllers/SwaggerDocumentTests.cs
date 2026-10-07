using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class SwaggerDocumentTests : IntegrationTestBase
    {
        public SwaggerDocumentTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private async Task<JsonElement> LoadDocumentAsync()
        {
            var client = Factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" }))).CreateClient();

            var response = await client.GetAsync("/swagger/v1/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        }

        [Fact]
        public async Task ThePasswordEndpoints_AreDocumentedWithTheirDescriptions()
        {
            var document = await LoadDocumentAsync();
            var paths = document.GetProperty("paths");

            foreach (var path in new[] { "forgot-password", "reset-password", "change-password" })
            {
                paths.EnumerateObject()
                    .Should().Contain(p => p.Name.EndsWith("/auth/" + path), $"{path} must be in the document");
            }

            var forgot = paths.EnumerateObject().First(p => p.Name.EndsWith("/auth/forgot-password")).Value.GetProperty("post");
            forgot.GetProperty("summary").GetString().Should().Contain("6-digit code");
        }

        [Fact]
        public async Task EveryOperation_DocumentsTheProblemResponses()
        {
            var document = await LoadDocumentAsync();

            foreach (var path in document.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    var responses = operation.Value.GetProperty("responses");
                    responses.TryGetProperty("400", out _).Should().BeTrue($"{operation.Name} {path.Name} documents 400");
                    responses.TryGetProperty("500", out _).Should().BeTrue($"{operation.Name} {path.Name} documents 500");
                }
            }
        }
    }
}
