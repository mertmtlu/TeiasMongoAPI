namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    /// <summary>
    /// Response DTO for file download token generation
    /// </summary>
    public class FileDownloadTokenResponseDto
    {
        /// <summary>
        /// Single-use download token
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }
}