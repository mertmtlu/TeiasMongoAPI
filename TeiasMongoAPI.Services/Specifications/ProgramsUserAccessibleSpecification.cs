using System.Linq.Expressions;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class ProgramsUserAccessibleSpecification : BaseSpecification<Program>
    {
        public ProgramsUserAccessibleSpecification(string userId, List<ObjectId>? userGroupIds, PaginationRequestDto pagination)
            : base(CreateCriteria(userId, userGroupIds))
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(p => p.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Program, bool>> CreateCriteria(string userId, List<ObjectId>? userGroupIds)
        {
            // Convert user group ObjectIds to strings for comparison
            var userGroupIdStrings = userGroupIds?.Select(id => id.ToString()).ToList() ?? new List<string>();

            // Filter programs where user has access:
            // 1. Public programs (IsPublic = true)
            // 2. Programs created by the user (CreatorId = userId)
            // 3. Programs with direct user permissions
            // 4. Programs with group permissions where user is a member
            return p => p.IsPublic == true || 
                       p.CreatorId == userId || 
                       p.Permissions.Users.Any(up => up.UserId == userId) ||
                       (userGroupIdStrings.Any() && p.Permissions.Groups.Any(gp => userGroupIdStrings.Contains(gp.GroupId)));
        }
    }
}