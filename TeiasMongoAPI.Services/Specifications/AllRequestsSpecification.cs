using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllRequestsSpecification : BaseSpecification<Request>
    {
        public AllRequestsSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by RequestedAt descending
            AddOrderByDescending(r => r.RequestedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}