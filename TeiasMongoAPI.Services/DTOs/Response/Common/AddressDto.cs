namespace TeiasMongoAPI.Services.DTOs.Response.Common
{
    public class AddressDto
    {
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
    }
}