namespace API.Models.Auth
{
    public class LogoutRequestDto
    {
        /// <summary>The refresh token of the device to sign out. Leave empty to sign out of every device.</summary>
        public string? RefreshToken { get; set; }
    }
}
