using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class WorkflowsUserAccessibleSpecification : BaseSpecification<Workflow>
    {
        public WorkflowsUserAccessibleSpecification(string userId, PaginationRequestDto pagination)
            : base(CreateCriteria(userId))
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(w => w.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Workflow, bool>> CreateCriteria(string userId)
        {
            // Filter workflows where user has access (public OR creator OR has permissions)
            return w => w.IsPublic == true || 
                       w.Creator == userId || 
                       w.Permissions.AllowedUsers.Contains(userId) ||
                       w.Permissions.Permissions.Any(p => p.UserId == userId);
        }
    }
}