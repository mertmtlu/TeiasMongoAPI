using System.Linq.Expressions;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;

namespace TeiasMongoAPI.Services.Specifications
{
    public class BuildingSearchSpecification : BaseSpecification<Building>
    {
        public BuildingSearchSpecification(BuildingSearchDto searchDto, PaginationRequestDto pagination)
            : base(CreateSearchCriteria(searchDto))
        {
            // Set default ordering by Name ascending
            AddOrderBy(b => b.Name);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<Building, bool>> CreateSearchCriteria(BuildingSearchDto searchDto)
        {
            Expression<Func<Building, bool>> criteria = b => true; // Start with "all"

            // Apply all filters using AND logic
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                var nameCriteria = (Expression<Func<Building, bool>>)(b => b.Name.Contains(searchDto.Name));
                criteria = CombineWithAnd(criteria, nameCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.TmId))
            {
                var tmObjectId = ObjectId.Parse(searchDto.TmId);
                var tmCriteria = (Expression<Func<Building, bool>>)(b => b.TmID == tmObjectId);
                criteria = CombineWithAnd(criteria, tmCriteria);
            }

            if (searchDto.Type.HasValue)
            {
                var typeCriteria = (Expression<Func<Building, bool>>)(b => b.Type == searchDto.Type.Value);
                criteria = CombineWithAnd(criteria, typeCriteria);
            }

            if (searchDto.InScopeOfMETU.HasValue)
            {
                var scopeCriteria = (Expression<Func<Building, bool>>)(b => b.InScopeOfMETU == searchDto.InScopeOfMETU.Value);
                criteria = CombineWithAnd(criteria, scopeCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.ReportName))
            {
                var reportCriteria = (Expression<Func<Building, bool>>)(b => b.ReportName.Contains(searchDto.ReportName));
                criteria = CombineWithAnd(criteria, reportCriteria);
            }

            return criteria;
        }

        private static Expression<Func<Building, bool>> CombineWithAnd(
            Expression<Func<Building, bool>> first,
            Expression<Func<Building, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(Building), "b");
            var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
            var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
            var combined = Expression.AndAlso(firstBody, secondBody);
            return Expression.Lambda<Func<Building, bool>>(combined, parameter);
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