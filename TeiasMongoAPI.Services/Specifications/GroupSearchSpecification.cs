using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Group;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Core.Specifications;

namespace TeiasMongoAPI.Services.Specifications
{
    public class GroupSearchSpecification : BaseSpecification<Group>
    {
        public GroupSearchSpecification(GroupSearchDto searchDto, PaginationRequestDto pagination)
            : base(BuildSearchCriteria(searchDto))
        {
            ApplyPaging(pagination.PageSize * (pagination.PageNumber - 1), pagination.PageSize);
            AddOrderByDescending(g => g.CreatedAt);
        }

        private static Expression<Func<Group, bool>> BuildSearchCriteria(GroupSearchDto searchDto)
        {
            var filters = new List<Expression<Func<Group, bool>>>();

            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                filters.Add(g => g.Name.ToLower().Contains(searchDto.Name.ToLower()));
            }

            if (!string.IsNullOrEmpty(searchDto.Description))
            {
                filters.Add(g => g.Description.ToLower().Contains(searchDto.Description.ToLower()));
            }

            if (!string.IsNullOrEmpty(searchDto.CreatedBy))
            {
                filters.Add(g => g.CreatedBy == searchDto.CreatedBy);
            }

            if (searchDto.IsActive.HasValue)
            {
                filters.Add(g => g.IsActive == searchDto.IsActive.Value);
            }

            if (searchDto.CreatedAfter.HasValue)
            {
                filters.Add(g => g.CreatedAt >= searchDto.CreatedAfter.Value);
            }

            if (searchDto.CreatedBefore.HasValue)
            {
                filters.Add(g => g.CreatedAt <= searchDto.CreatedBefore.Value);
            }

            if (searchDto.HasMembers.HasValue)
            {
                if (searchDto.HasMembers.Value)
                {
                    filters.Add(g => g.Members.Count > 0);
                }
                else
                {
                    filters.Add(g => g.Members.Count == 0);
                }
            }

            if (searchDto.MinMemberCount.HasValue)
            {
                filters.Add(g => g.Members.Count >= searchDto.MinMemberCount.Value);
            }

            if (searchDto.MaxMemberCount.HasValue)
            {
                filters.Add(g => g.Members.Count <= searchDto.MaxMemberCount.Value);
            }

            // Combine all filters with AND logic
            if (!filters.Any())
            {
                return g => true;
            }

            var combinedFilter = filters[0];
            for (int i = 1; i < filters.Count; i++)
            {
                var filter = filters[i];
                combinedFilter = CombineWithAnd(combinedFilter, filter);
            }

            return combinedFilter;
        }

        private static Expression<Func<Group, bool>> CombineWithAnd(
            Expression<Func<Group, bool>> left,
            Expression<Func<Group, bool>> right)
        {
            var parameter = Expression.Parameter(typeof(Group));
            var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
            var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
            var combined = Expression.AndAlso(leftBody, rightBody);
            return Expression.Lambda<Func<Group, bool>>(combined, parameter);
        }

        private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
        }
    }

    internal class ParameterReplacer : ExpressionVisitor
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