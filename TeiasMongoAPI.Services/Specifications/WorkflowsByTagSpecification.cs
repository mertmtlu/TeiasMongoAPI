using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class WorkflowsByTagSpecification : BaseSpecification<Workflow>
    {
        public WorkflowsByTagSpecification(string tag, PaginationRequestDto pagination)
            : base(w => w.Tags.Contains(tag))
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