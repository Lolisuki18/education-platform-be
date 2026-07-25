namespace API.Models.Common
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string message = "Request successful", int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                IsSuccess = true,
                StatusCode = statusCode,
                Message = message,
                Data = data
            };
        }
    }

    public class ApiResponse
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;

        public static ApiResponse Success(string message = "Request successful", int statusCode = 200)
        {
            return new ApiResponse
            {
                IsSuccess = true,
                StatusCode = statusCode,
                Message = message
            };
        }

        public static ApiResponse Error(string message, int statusCode = 400)
        {
            return new ApiResponse
            {
                IsSuccess = false,
                StatusCode = statusCode,
                Message = message
            };
        }
    }

    public class PagedApiResponse<T> : ApiResponse<IEnumerable<T>>
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }

        public static PagedApiResponse<T> Success(IEnumerable<T> data, int pageNumber, int pageSize, int totalRecords, string message = "Request successful", int statusCode = 200)
        {
            var totalPages = totalRecords > 0 ? (int)Math.Ceiling(totalRecords / (double)pageSize) : 0;
            return new PagedApiResponse<T>
            {
                IsSuccess = true,
                StatusCode = statusCode,
                Message = message,
                Data = data,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalRecords = totalRecords
            };
        }
    }
}
