using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Results;
using FluentAssertions;
using IntegrationTests;
using Xunit;

namespace FunctionalTests
{
    public class UserProfileFlowTests : IntegrationTestBase
    {
        public UserProfileFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task UpdateUserProfileAndVerifyFlow_ShouldSucceed()
        {
            var email = "profile_flow@example.com";
            var password = "Password123!";
            var phone = "0987111222";
            var name = "Profile Flow User";

            // 1. Register & Verify & Login
            var authResult = await AuthenticateAsync(email, password, name, phone, 1);

            // 2. Query initial profile
            var profileResponse = await Client.GetAsync("/api/user/me");
            profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var profileResult = await profileResponse.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            profileResult.Should().NotBeNull();
            profileResult!.Data.Should().NotBeNull();
            profileResult.Data!.Name.Should().Be(name);
            profileResult.Data.Phone.Should().Be(phone);
            profileResult.Data.Bio.Should().Be("Some Bio"); // AuthenticateAsync registers user with hardcoded "Some Bio"

            // 3. Update profile details
            var updateRequest = new API.Models.Users.UpdateProfileRequest
            {
                Name = "Updated Name",
                Phone = "0911222333",
                Bio = "Updated Bio"
            };
            var updateResponse = await Client.PatchAsJsonAsync("/api/user/update-profile", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify detail is updated in the returned payload
            var updateResult = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            updateResult.Should().NotBeNull();
            updateResult!.Data.Should().NotBeNull();
            updateResult.Data!.Name.Should().Be("Updated Name");
            updateResult.Data.Phone.Should().Be("0911222333");
            updateResult.Data.Bio.Should().Be("Updated Bio");

            // 5. Query /me again to assert persistence
            var finalProfileResponse = await Client.GetAsync("/api/user/me");
            var finalProfileResult = await finalProfileResponse.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            finalProfileResult!.Data!.Name.Should().Be("Updated Name");

            // 6. Test: sending empty/null updates must preserve existing values
            var emptyRequest = new API.Models.Users.UpdateProfileRequest
            {
                Name = "",
                Phone = null,
                Bio = "   "
            };
            var emptyUpdateResponse = await Client.PatchAsJsonAsync("/api/user/update-profile", emptyRequest);
            emptyUpdateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var emptyUpdateResult = await emptyUpdateResponse.Content.ReadFromJsonAsync<ApiResponse<UserDTO>>();
            emptyUpdateResult!.Data!.Name.Should().Be("Updated Name");
            emptyUpdateResult.Data.Phone.Should().Be("0911222333");
            emptyUpdateResult.Data.Bio.Should().Be("Updated Bio");
        }
    }
}
