namespace API.Helpers
{
    /// <summary>
    /// <c>dotnet API.dll --healthcheck</c>: asks the running instance of this same application for <c>/healthz</c>
    /// and reports the result as an exit code. It exists for container images that ship without curl or a shell.
    /// </summary>
    public static class HealthProbe
    {
        public static async Task<int> RunAsync(string? aspNetCoreUrls, HttpMessageHandler? handler = null)
        {
            try
            {
                using var client = handler == null ? new HttpClient() : new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(4);

                var response = await client.GetAsync(ResolveHealthUrl(aspNetCoreUrls));
                return response.IsSuccessStatusCode ? 0 : 1;
            }
            catch
            {
                return 1;
            }
        }

        /// <summary>
        /// The first configured address, with wildcard hosts (<c>+</c>, <c>*</c>, <c>0.0.0.0</c>) replaced by
        /// localhost, which is where the probe runs. Falls back to port 8080, the one the image listens on.
        /// </summary>
        public static Uri ResolveHealthUrl(string? aspNetCoreUrls)
        {
            var first = (aspNetCoreUrls ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(first))
                return new Uri("http://localhost:8080/healthz");

            var normalized = first
                .Replace("://+", "://localhost", StringComparison.Ordinal)
                .Replace("://*", "://localhost", StringComparison.Ordinal)
                .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
                .Replace("://[::]", "://localhost", StringComparison.Ordinal);

            return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                ? new Uri(uri, "/healthz")
                : new Uri("http://localhost:8080/healthz");
        }
    }
}
