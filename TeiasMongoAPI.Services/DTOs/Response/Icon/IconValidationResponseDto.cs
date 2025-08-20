namespace TeiasMongoAPI.Services.DTOs.Response.Icon
{
    public class IconValidationResponseDto
    {
        public bool IsValid { get; set; }
        public required string Message { get; set; }
    }
}