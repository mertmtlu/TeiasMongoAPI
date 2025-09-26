using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using ExecutionModel = TeiasMongoAPI.Core.Models.Collaboration.Execution;

namespace TeiasMongoAPI.Services.Specifications
{
    public class UserFilteredExecutionsSpecification : BaseSpecification<ExecutionModel>
    {
        public UserFilteredExecutionsSpecification(string? userId, PaginationRequestDto pagination)
            : base(CreateUserFilterCriteria(userId))
        {
            // Set default ordering by StartedAt descending
            AddOrderByDescending(e => e.StartedAt);

            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<ExecutionModel, bool>> CreateUserFilterCriteria(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                // Return all executions if no userId is specified (admin users)
                return e => true;
            }
            else
            {
                // Filter by userId if specified (non-admin users)
                return e => e.UserId == userId;
            }
        }
    }
}