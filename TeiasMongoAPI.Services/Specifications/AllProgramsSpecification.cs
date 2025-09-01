using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class AllProgramsSpecification : BaseSpecification<Program>
    {
        public AllProgramsSpecification(PaginationRequestDto pagination)
            : base()
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(p => p.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        public AllProgramsSpecification(PaginationRequestDto pagination, List<ObjectId> allowedProgramIds)
            : base()
        {
            // Filter by allowed program IDs
            AddCriteria(p => allowedProgramIds.Contains(p._ID));
            
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