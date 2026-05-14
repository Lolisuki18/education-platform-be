namespace Application.Interface
{
    public interface IPaymentService
    {
        Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description);
    }
}
