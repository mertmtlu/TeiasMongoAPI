using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Pagination
{
    public class SortingRequestDto
    {
        [Required]
        public required string Field { get; set; }

        public SortDirection Direction { get; set; } = SortDirection.Ascending;
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}