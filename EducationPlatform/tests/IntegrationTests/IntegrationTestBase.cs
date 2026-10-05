using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Auth;
using API.Models.Common;
using Domain.IdentityManagement.Aggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IntegrationTests
{
    [Collection("Shared database collection")]
    public class IntegrationTestBase : IAsyncLifetime
    {
        protected readonly CustomWebApplicationFactory Factory;
        protected readonly HttpClient Client;

        protected IntegrationTestBase(CustomWebApplicationFactory factory)
        {
            Factory = factory;
            Client = factory.CreateClient();
        }

        public virtual async Task InitializeAsync()
        {
            await Factory.ResetDatabaseAsync();
        }

        public virtual Task DisposeAsync() => Task.CompletedTask;

        // Helper to register, verify, and authenticate a client
        protected async Task<(string Token, Guid UserId)> AuthenticateAsync(
            string email = "testuser@example.com",
            string password = "Password123!",
            string name = "Test User",
            string phone = "0987654321",
            int role = 1) // 1 = Student, 2 = Teacher
        {
            // 1. Register
            var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", new
            {
                Email = email,
                Password = password,
                Phone = phone,
                Name = name,
                Bio = "Some Bio",
                Role = role
            });
            registerResponse.EnsureSuccessStatusCode();

            // 2. Fetch OTP from DB
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
            var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || string.IsNullOrEmpty(user.EmailOtp))
            {
                throw new Exception("User registration failed or OTP was not generated.");
            }

            var otp = user.EmailOtp;
            var userId = user.UserID;

            // 3. Verify Email
            var verifyResponse = await Client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequestDto
            {
                Email = email,
                Otp = otp
            });
            verifyResponse.EnsureSuccessStatusCode();

            // 4. Login
            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });
            loginResponse.EnsureSuccessStatusCode();

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            if (loginResult == null || loginResult.Data == null)
            {
                throw new Exception("Login failed to return valid token.");
            }

            var token = loginResult.Data.AccessToken;

            // 5. Apply Token to Client
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return (token, userId);
        }

        protected async Task<string> LoginExistingUserAsync(string email, string password)
        {
            var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });
            loginResponse.EnsureSuccessStatusCode();

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            if (loginResult == null || loginResult.Data == null)
            {
                throw new Exception("Login failed to return valid token.");
            }

            var token = loginResult.Data.AccessToken;
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return token;
        }

        protected async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
        {
            var client = Factory.CreateClient();
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
            {
                Email = email,
                Password = password
            });
            loginResponse.EnsureSuccessStatusCode();

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>();
            if (loginResult == null || loginResult.Data == null)
            {
                throw new Exception("Login failed to return valid token.");
            }

            var token = loginResult.Data.AccessToken;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        /// <summary>
        /// Plays the browser redirect PayOS performs after payment. The redirect is not followed, so the
        /// test can assert where the API sends the user.
        /// </summary>
        protected async Task<HttpResponseMessage> ReturnFromPayOsAsync(long orderCode, string status = "PAID", bool cancelled = false)
        {
            var client = Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // The signature is checked by a fake verifier in the test host, but it must be present
            return await client.GetAsync(
                $"/api/orders/return?status={status}&orderCode={orderCode}&cancel={(cancelled ? "true" : "false")}&signature=test-signature");
        }

        protected async Task ExecuteDbContextAsync(Func<EducationPlatformDBContext, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
            await action(db);
        }

        protected async Task<T> ExecuteDbContextAsync<T>(Func<EducationPlatformDBContext, Task<T>> action)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
            return await action(db);
        }

        protected MultipartFormDataContent CreateMultipartFormContent(
            Dictionary<string, string> fields,
            byte[]? fileBytes = null,
            string? paramName = null,
            string? fileName = null,
            string? contentType = null)
        {
            var content = new MultipartFormDataContent();

            foreach (var kvp in fields)
            {
                content.Add(new StringContent(kvp.Value), kvp.Key);
            }

            if (fileBytes != null && !string.IsNullOrEmpty(paramName))
            {
                var fileContent = new ByteArrayContent(fileBytes);
                if (!string.IsNullOrEmpty(contentType))
                {
                    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                }
                content.Add(fileContent, paramName, fileName ?? "file");
            }

            return content;
        }
    }
}
