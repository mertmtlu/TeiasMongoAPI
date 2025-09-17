namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class NamedPointDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}