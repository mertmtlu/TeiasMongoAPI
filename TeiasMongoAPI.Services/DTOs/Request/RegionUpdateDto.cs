namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class RegionUpdateDto
    {
        public List<string>? Cities { get; set; }
        public string? Headquarters { get; set; }
    }
}