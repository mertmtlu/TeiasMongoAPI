namespace TeiasMongoAPI.Services.DTOs.Response.Search
{
    public class SearchResultResponseDto<T>
    {
        public List<T> Results { get; set; } = new();
        public int TotalMatches { get; set; }
        public string Query { get; set; } = string.Empty;
        public double SearchTime { get; set; }
    }

    public class AutocompleteResultDto
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}