using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;

namespace TeiasMongoAPI.Services.Specifications
{
    public class UserSearchSpecification : BaseSpecification<User>
    {
        public UserSearchSpecification(UserSearchDto searchDto, PaginationRequestDto pagination)
            : base(CreateSearchCriteria(searchDto))
        {
            // Set default ordering by CreatedDate descending
            AddOrderByDescending(u => u.CreatedDate);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<User, bool>> CreateSearchCriteria(UserSearchDto searchDto)
        {
            Expression<Func<User, bool>> criteria = u => true; // Start with "all"

            // Apply all filters using AND logic
            if (!string.IsNullOrEmpty(searchDto.Email))
            {
                var emailCriteria = (Expression<Func<User, bool>>)(u => u.Email.Contains(searchDto.Email));
                criteria = CombineWithAnd(criteria, emailCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Username))
            {
                var usernameCriteria = (Expression<Func<User, bool>>)(u => u.Username.Contains(searchDto.Username));
                criteria = CombineWithAnd(criteria, usernameCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.FirstName))
            {
                var firstNameCriteria = (Expression<Func<User, bool>>)(u => u.FirstName.Contains(searchDto.FirstName));
                criteria = CombineWithAnd(criteria, firstNameCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.LastName))
            {
                var lastNameCriteria = (Expression<Func<User, bool>>)(u => u.LastName.Contains(searchDto.LastName));
                criteria = CombineWithAnd(criteria, lastNameCriteria);
            }

            if (searchDto.Roles?.Any() == true)
            {
                var rolesCriteria = (Expression<Func<User, bool>>)(u => u.Roles.Any(r => searchDto.Roles.Contains(r)));
                criteria = CombineWithAnd(criteria, rolesCriteria);
            }

            if (searchDto.IsActive.HasValue)
            {
                var activeCriteria = (Expression<Func<User, bool>>)(u => u.IsActive == searchDto.IsActive.Value);
                criteria = CombineWithAnd(criteria, activeCriteria);
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                var fromCriteria = (Expression<Func<User, bool>>)(u => u.CreatedDate >= searchDto.CreatedFrom.Value);
                criteria = CombineWithAnd(criteria, fromCriteria);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                var toCriteria = (Expression<Func<User, bool>>)(u => u.CreatedDate <= searchDto.CreatedTo.Value);
                criteria = CombineWithAnd(criteria, toCriteria);
            }

            if (searchDto.LastLoginFrom.HasValue)
            {
                var loginFromCriteria = (Expression<Func<User, bool>>)(u => u.LastLoginDate >= searchDto.LastLoginFrom.Value);
                criteria = CombineWithAnd(criteria, loginFromCriteria);
            }

            if (searchDto.LastLoginTo.HasValue)
            {
                var loginToCriteria = (Expression<Func<User, bool>>)(u => u.LastLoginDate <= searchDto.LastLoginTo.Value);
                criteria = CombineWithAnd(criteria, loginToCriteria);
            }

            return criteria;
        }

        private static Expression<Func<User, bool>> CombineWithAnd(
            Expression<Func<User, bool>> first,
            Expression<Func<User, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(User), "u");
            var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
            var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
            var combined = Expression.AndAlso(firstBody, secondBody);
            return Expression.Lambda<Func<User, bool>>(combined, parameter);
        }

        private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParam, ParameterExpression newParam)
        {
            return new ParameterReplacer(oldParam, newParam).Visit(expression);
        }

        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return node == _oldParameter ? _newParameter : base.VisitParameter(node);
            }
        }
    }
}