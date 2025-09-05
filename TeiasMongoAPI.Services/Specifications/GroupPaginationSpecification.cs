using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Core.Specifications;

namespace TeiasMongoAPI.Services.Specifications
{
    public class GroupPaginationSpecification : BaseSpecification<Group>
    {
        public GroupPaginationSpecification(PaginationRequestDto pagination)
            : base(g => true)
        {
            ApplyPaging(pagination.PageSize * (pagination.PageNumber - 1), pagination.PageSize);
            
            // Default ordering by creation date (newest first)
            AddOrderByDescending(g => g.CreatedAt);
        }
    }
}