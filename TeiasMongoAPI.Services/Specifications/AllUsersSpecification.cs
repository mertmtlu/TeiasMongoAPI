using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllUsersSpecification : BaseSpecification<User>
    {
        public AllUsersSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by CreatedDate descending
            AddOrderByDescending(u => u.CreatedDate);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}