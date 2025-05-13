namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class BlockResponseDto
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ModelingType { get; set; } = string.Empty; // "Masonry" or "Concrete"
        public double XAxisLength { get; set; }
        public double YAxisLength { get; set; }
        public Dictionary<int, double> StoreyHeight { get; set; } = new();
        public double LongLength { get; set; } // Computed
        public double ShortLength { get; set; } // Computed
        public double TotalHeight { get; set; } // Computed
    }
}
