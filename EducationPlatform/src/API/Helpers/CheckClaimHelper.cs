using System.Security.Claims;

namespace API.Helpers
{
    public static class CheckClaimHelper
    {
        public static (Guid userId, string role) CheckClaim(ClaimsPrincipal user)
        {
            if (user == null || !user.Identity?.IsAuthenticated == true)
                throw new Exception("User is not authenticated.");

            // ----- User ID -----
            var userIdString = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdString))
                throw new Exception("User ID not found in claims.");

            if (!Guid.TryParse(userIdString, out var userId))
                throw new Exception("User ID claim is not a valid GUID.");

            // ----- Role -----
            var role = user.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrWhiteSpace(role))
                throw new Exception("Role not found in claims.");

            return (userId, role);
        }
    }
}

