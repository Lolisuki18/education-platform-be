using System.ComponentModel.DataAnnotations;

namespace Application.Options
{
    public class PayOSOptions
    {
        public const string SectionName = "PayOS";

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string ChecksumKey { get; set; } = string.Empty;

        /// <summary>Public URL PayOS redirects the browser to after payment (this API's /api/orders/return).</summary>
        [Required]
        public string ReturnUrl { get; set; } = string.Empty;

        [Required]
        public string CancelUrl { get; set; } = string.Empty;

        [Required]
        public string FrontendUrl { get; set; } = "http://localhost:3000";
    }
}
