using Domain.IdentityManagement.Aggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Application.Helper
{
    public static class TokenGenerator
    {
        public static string GenerateToken(User user, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("JwtSettings");

            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("Missing JwtSettings:SecretKey");
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            var expiryMinutesStr = jwtSettings["ExpiryMinutes"] ?? "60";
            var expiryMinutes = double.Parse(expiryMinutesStr);
            var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var expirySecondsStr = jwtSettings["ExpirySeconds"];
            if (!string.IsNullOrEmpty(expirySecondsStr) && double.TryParse(expirySecondsStr, out var expirySeconds))
            {
                expires = DateTime.UtcNow.AddSeconds(expirySeconds);
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };


            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            return Convert.ToBase64String(randomBytes);
        }
    }
}


