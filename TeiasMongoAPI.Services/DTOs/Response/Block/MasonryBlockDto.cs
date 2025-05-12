namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class MasonryBlockDto : BlockDto
    {
        public List<MasonryUnitTypeDto> UnitTypeList { get; set; } = new();
    }
}