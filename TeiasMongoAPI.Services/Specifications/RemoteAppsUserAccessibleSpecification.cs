using System.Linq.Expressions;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class RemoteAppsUserAccessibleSpecification : BaseSpecification<RemoteApp>
    {
        public RemoteAppsUserAccessibleSpecification(string userId, List<ObjectId>? userGroupIds, PaginationRequestDto pagination)
            : base(CreateCriteria(userId, userGroupIds))
        {
            // Default ordering by creation date descending (newest first)
            AddOrderByDescending(r => r.CreatedAt);

            // Apply pagination with correct skip calculation
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<RemoteApp, bool>> CreateCriteria(string userId, List<ObjectId>? userGroupIds)
        {
            // Convert user group ObjectIds to strings for comparison
            var userGroupIdStrings = userGroupIds?.Select(id => id.ToString()).ToList() ?? new List<string>();

            // Filter remote apps where user has access:
            // 1. Public remote apps (IsPublic = true)
            // 2. Remote apps created by the user (Creator = userId)
            // 3. Remote apps with direct user permissions
            // 4. Remote apps with group permissions where user is a member
            return r => r.IsPublic == true ||
                       r.Creator == userId ||
                       r.Permissions.Users.Any(up => up.UserId == userId) ||
                       (userGroupIdStrings.Any() && r.Permissions.Groups.Any(gp => userGroupIdStrings.Contains(gp.GroupId)));
        }
    }
}