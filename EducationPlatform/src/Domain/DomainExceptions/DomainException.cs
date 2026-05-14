namespace Domain.DomainExceptions
{
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }
}

