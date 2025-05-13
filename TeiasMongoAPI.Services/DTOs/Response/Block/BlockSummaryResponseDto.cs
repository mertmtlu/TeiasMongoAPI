namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class BlockSummaryResponseDto
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ModelingType { get; set; } = string.Empty;
        public double TotalHeight { get; set; }
        public int StoreyCount { get; set; }
    }
}