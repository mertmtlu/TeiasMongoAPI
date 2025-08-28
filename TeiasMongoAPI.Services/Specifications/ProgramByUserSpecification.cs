using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class ProgramByUserSpecification : BaseSpecification<Program>
    {
        public ProgramByUserSpecification(string userId, PaginationRequestDto pagination)
            : base(CreateCriteria(userId))
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(p => p.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Program, bool>> CreateCriteria(string userId)
        {
            // Filter programs where user is creator OR has user permissions
            return p => p.CreatorId == userId || 
                       p.Permissions.Users.Any(up => up.UserId == userId);
        }
    }
}