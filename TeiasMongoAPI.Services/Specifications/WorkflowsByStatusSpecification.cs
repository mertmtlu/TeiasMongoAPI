using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class WorkflowsByStatusSpecification : BaseSpecification<Workflow>
    {
        public WorkflowsByStatusSpecification(WorkflowStatus status, PaginationRequestDto pagination)
            : base(w => w.Status == status)
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(w => w.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}