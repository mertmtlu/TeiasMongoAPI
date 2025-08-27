using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using ExecutionModel = TeiasMongoAPI.Core.Models.Collaboration.Execution;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllExecutionsSpecification : BaseSpecification<ExecutionModel>
    {
        public AllExecutionsSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by StartedAt descending
            AddOrderByDescending(e => e.StartedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}