using System.Linq.Expressions;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using ExecutionModel = TeiasMongoAPI.Core.Models.Collaboration.Execution;

namespace TeiasMongoAPI.Services.Specifications
{
    public class ExecutionSearchSpecification : BaseSpecification<ExecutionModel>
    {
        public ExecutionSearchSpecification(ExecutionSearchDto searchDto, PaginationRequestDto pagination)
            : base(CreateSearchCriteria(searchDto))
        {
            // Set default ordering by StartedAt descending
            AddOrderByDescending(e => e.StartedAt);
            
            // Apply pagination
            ApplyPaging(
                skip: (pagination.PageNumber - 1) * pagination.PageSize,
                take: pagination.PageSize
            );
        }

        private static Expression<Func<ExecutionModel, bool>> CreateSearchCriteria(ExecutionSearchDto searchDto)
        {
            Expression<Func<ExecutionModel, bool>> criteria = e => true; // Start with "all"

            // Apply all filters using AND logic
            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ObjectId.Parse(searchDto.ProgramId);
                var programCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.ProgramId == programObjectId);
                criteria = CombineWithAnd(criteria, programCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.VersionId))
            {
                var versionObjectId = ObjectId.Parse(searchDto.VersionId);
                var versionCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.VersionId == versionObjectId);
                criteria = CombineWithAnd(criteria, versionCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.UserId))
            {
                var userCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.UserId == searchDto.UserId);
                criteria = CombineWithAnd(criteria, userCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                var statusCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.Status == searchDto.Status);
                criteria = CombineWithAnd(criteria, statusCriteria);
            }

            if (!string.IsNullOrEmpty(searchDto.ExecutionType))
            {
                var typeCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.ExecutionType == searchDto.ExecutionType);
                criteria = CombineWithAnd(criteria, typeCriteria);
            }

            if (searchDto.StartedFrom.HasValue)
            {
                var startFromCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.StartedAt >= searchDto.StartedFrom.Value);
                criteria = CombineWithAnd(criteria, startFromCriteria);
            }

            if (searchDto.StartedTo.HasValue)
            {
                var startToCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.StartedAt <= searchDto.StartedTo.Value);
                criteria = CombineWithAnd(criteria, startToCriteria);
            }

            if (searchDto.CompletedFrom.HasValue)
            {
                var completedFromCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.CompletedAt >= searchDto.CompletedFrom.Value);
                criteria = CombineWithAnd(criteria, completedFromCriteria);
            }

            if (searchDto.CompletedTo.HasValue)
            {
                var completedToCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.CompletedAt <= searchDto.CompletedTo.Value);
                criteria = CombineWithAnd(criteria, completedToCriteria);
            }

            if (searchDto.ExitCodeFrom.HasValue)
            {
                var exitCodeFromCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.Results.ExitCode >= searchDto.ExitCodeFrom.Value);
                criteria = CombineWithAnd(criteria, exitCodeFromCriteria);
            }

            if (searchDto.ExitCodeTo.HasValue)
            {
                var exitCodeToCriteria = (Expression<Func<ExecutionModel, bool>>)(e => e.Results.ExitCode <= searchDto.ExitCodeTo.Value);
                criteria = CombineWithAnd(criteria, exitCodeToCriteria);
            }

            if (searchDto.HasErrors.HasValue)
            {
                if (searchDto.HasErrors.Value)
                {
                    var hasErrorsCriteria = (Expression<Func<ExecutionModel, bool>>)(e => !string.IsNullOrEmpty(e.Results.Error) || e.Results.ExitCode != 0);
                    criteria = CombineWithAnd(criteria, hasErrorsCriteria);
                }
                else
                {
                    var noErrorsCriteria = (Expression<Func<ExecutionModel, bool>>)(e => string.IsNullOrEmpty(e.Results.Error) && e.Results.ExitCode == 0);
                    criteria = CombineWithAnd(criteria, noErrorsCriteria);
                }
            }

            return criteria;
        }

        private static Expression<Func<ExecutionModel, bool>> CombineWithAnd(
            Expression<Func<ExecutionModel, bool>> first,
            Expression<Func<ExecutionModel, bool>> second)
        {
            var parameter = Expression.Parameter(typeof(ExecutionModel), "e");
            var firstBody = ReplaceParameter(first.Body, first.Parameters[0], parameter);
            var secondBody = ReplaceParameter(second.Body, second.Parameters[0], parameter);
            var combined = Expression.AndAlso(firstBody, secondBody);
            return Expression.Lambda<Func<ExecutionModel, bool>>(combined, parameter);
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