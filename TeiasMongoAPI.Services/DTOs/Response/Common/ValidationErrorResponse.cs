namespace TeiasMongoAPI.Services.DTOs.Response.Common
{
    public class ValidationErrorResponse : ErrorResponse
    {
        public Dictionary<string, List<string>> FieldErrors { get; set; } = new();

        public ValidationErrorResponse()
        {
            ErrorCode = "VALIDATION_ERROR";
            Message = "One or more validation errors occurred.";
        }
    }
}