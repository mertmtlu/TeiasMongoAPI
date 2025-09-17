namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class FileDataDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Checksum { get; set; }
        public string? Base64Content { get; set; }
        public string? Filename { get; set; }
    }
}