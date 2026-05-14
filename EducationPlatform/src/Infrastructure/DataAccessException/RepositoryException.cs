namespace Infrastructure.DataAccessException
{
    public class RepositoryException : Exception
    {
        public RepositoryException(string message) : base(message) { }
    }
}

