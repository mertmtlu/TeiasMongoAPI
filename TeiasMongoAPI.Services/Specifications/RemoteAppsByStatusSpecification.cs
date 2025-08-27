using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class RemoteAppsByStatusSpecification : BaseSpecification<RemoteApp>
    {
        public RemoteAppsByStatusSpecification(string status, PaginationRequestDto pagination) 
            : base(r => r.Status == status)
        {
            // Default ordering by creation date descending (newest first)
            AddOrderByDescending(r => r.CreatedAt);
            
            // Apply pagination with correct skip calculation
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}