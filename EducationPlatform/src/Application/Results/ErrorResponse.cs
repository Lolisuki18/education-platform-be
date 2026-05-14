namespace Application.Results
{
    public enum ErrorCode
    {
        Unauthorized = 401,
        NotFound = 404,
        Conflict = 409,
        BadRequest = 400,
        InternalServerError = 500
    }

    public class ErrorResponse
    {
        public ErrorCode Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public int StatusCode { get; set; }
        public bool ShowPopup { get; set; } = false;
    }
}


