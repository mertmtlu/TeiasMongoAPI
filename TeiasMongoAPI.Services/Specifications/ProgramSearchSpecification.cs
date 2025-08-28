using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class ProgramSearchSpecification : BaseSpecification<Program>
    {
        public ProgramSearchSpecification(ProgramSearchDto searchDto, PaginationRequestDto pagination)
            : base(CreateSearchCriteria(searchDto))
        {
            // Set default ordering by CreatedAt descending
            AddOrderByDescending(p => p.CreatedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Program, bool>> CreateSearchCriteria(ProgramSearchDto searchDto)
        {
            Expression<Func<Program, bool>> criteria = p => true; // Start with "all"

            // Apply all filters using AND logic
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                var nameCriteria = (Expression<Func<Program, bool>>)(p => p.Name.Contains(searchDto.Name));
                criteria = CombineWithAnd(criteria, nameCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Description))
            {
                var descCriteria = (Expression<Func<Program, bool>>)(p => p.Description.Contains(searchDto.Description));
                criteria = CombineWithAnd(criteria, descCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                var typeCriteria = (Expression<Func<Program, bool>>)(p => p.Type == searchDto.Type);
                criteria = CombineWithAnd(criteria, typeCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Language))
            {
                var langCriteria = (Expression<Func<Program, bool>>)(p => p.Language == searchDto.Language);
                criteria = CombineWithAnd(criteria, langCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.UiType))
            {
                var uiCriteria = (Expression<Func<Program, bool>>)(p => p.UiType == searchDto.UiType);
                criteria = CombineWithAnd(criteria, uiCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Creator))
            {
                var creatorCriteria = (Expression<Func<Program, bool>>)(p => p.CreatorId == searchDto.Creator);
                criteria = CombineWithAnd(criteria, creatorCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                var statusCriteria = (Expression<Func<Program, bool>>)(p => p.Status == searchDto.Status);
                criteria = CombineWithAnd(criteria, statusCriteria);
            }

            if (searchDto.DeploymentType.HasValue)
            {
                var deploymentCriteria = (Expression<Func<Program, bool>>)(p => p.DeploymentInfo != null && p.DeploymentInfo.DeploymentType == searchDto.DeploymentType.Value);
                criteria = CombineWithAnd(criteria, deploymentCriteria);
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                var fromCriteria = (Expression<Func<Program, bool>>)(p => p.CreatedAt >= searchDto.CreatedFrom.Value);
                criteria = CombineWithAnd(criteria, fromCriteria);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                var toCriteria = (Expression<Func<Program, bool>>)(p => p.CreatedAt <= searchDto.CreatedTo.Value);
                criteria = CombineWithAnd(criteria, toCriteria);
            }

            return criteria;
        }

        private static Expression<Func<Program, bool>> CombineWithAnd(
            Expression<Func<Program, bool>> first,
            Expression<Func<Program, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(Program), "p");
            var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
            var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
            var combined = Expression.AndAlso(firstBody, secondBody);
            return Expression.Lambda<Func<Program, bool>>(combined, parameter);
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