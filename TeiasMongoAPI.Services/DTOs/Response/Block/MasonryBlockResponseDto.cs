namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class MasonryBlockResponseDto : BlockResponseDto
    {
        public List<MasonryUnitTypeResponseDto> UnitTypeList { get; set; } = new();
    }
}