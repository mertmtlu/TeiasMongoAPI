namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class RegionCreateDto
    {
        public required string ClientId { get; set; } // Will be converted to ObjectId in service
        public required int Id { get; set; }  // Region number
        public required List<string> Cities { get; set; }
        public required string Headquarters { get; set; }
    }
}