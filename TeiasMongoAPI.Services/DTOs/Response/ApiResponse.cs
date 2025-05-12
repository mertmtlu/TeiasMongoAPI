namespace TeiasMongoAPI.Services.DTOs.Response
{
    // Generic API response wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }
        public List<string> Errors { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> Ok(T data, string? message = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ApiResponse<T> Fail(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Errors = errors ?? new List<string>()
            };
        }
    }

    // Pagination metadata
    public class PaginationMetadata
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }

    // Paginated response
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public PaginationMetadata Metadata { get; set; } = new();
    }

    // Paginated API response
    public class ApiPagedResponse<T> : ApiResponse<PagedResponse<T>>
    {
        public static ApiPagedResponse<T> Ok(List<T> items, PaginationMetadata metadata, string? message = null)
        {
            return new ApiPagedResponse<T>
            {
                Success = true,
                Data = new PagedResponse<T>
                {
                    Items = items,
                    Metadata = metadata
                },
                Message = message
            };
        }
    }

    // Error response
    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, List<string>> ValidationErrors { get; set; } = new();
        public string? TraceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Health check response
    public class HealthCheckResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Services { get; set; } = new();
    }
}