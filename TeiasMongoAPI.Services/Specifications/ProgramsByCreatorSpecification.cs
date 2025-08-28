using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class ProgramsByCreatorSpecification : BaseSpecification<Program>
    {
        public ProgramsByCreatorSpecification(string creatorId, PaginationRequestDto pagination)
            : base(p => p.CreatorId == creatorId)
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(p => p.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }
    }
}