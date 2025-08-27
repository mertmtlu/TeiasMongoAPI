using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class GroupAccessibleProgramsSpecification : BaseSpecification<Program>
    {
        public GroupAccessibleProgramsSpecification(string groupId, PaginationRequestDto pagination)
            : base(p => p.Permissions.Groups.Any(gp => gp.GroupId == groupId))
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