namespace API.Models.Orders
{
    public class CreateOrderResponseDto
    {
        /// <summary>PayOS payment page, or the frontend success page when nothing has to be paid.</summary>
        public string CheckoutUrl { get; set; } = string.Empty;

        /// <summary>False for free or fully discounted orders, which are paid and enrolled immediately.</summary>
        public bool RequiresPayment { get; set; } = true;
    }
}
