using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Interfaces.Specifications;

namespace TeiasMongoAPI.Core.Specifications
{
    public abstract class BaseSpecification<T> : ISpecification<T>
    {
        public Expression<Func<T, bool>>? Criteria { get; private set; }
        public List<Expression<Func<T, object>>> Includes { get; } = new List<Expression<Func<T, object>>>();
        public Expression<Func<T, object>>? OrderBy { get; private set; }
        public Expression<Func<T, object>>? OrderByDescending { get; private set; }
        public List<Expression<Func<T, object>>> ThenByExpressions { get; } = new List<Expression<Func<T, object>>>();
        public List<Expression<Func<T, object>>> ThenByDescendingExpressions { get; } = new List<Expression<Func<T, object>>>();

        public int Take { get; private set; }
        public int Skip { get; private set; }
        public bool IsPagingEnabled { get; private set; } = false;

        protected BaseSpecification()
        {
        }

        protected BaseSpecification(Expression<Func<T, bool>> criteria)
        {
            Criteria = criteria;
        }

        protected virtual void AddInclude(Expression<Func<T, object>> includeExpression)
        {
            Includes.Add(includeExpression);
        }

        protected virtual void AddOrderBy(Expression<Func<T, object>> orderByExpression)
        {
            OrderBy = orderByExpression;
        }

        protected virtual void AddOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
        {
            OrderByDescending = orderByDescExpression;
        }

        protected virtual void AddThenBy(Expression<Func<T, object>> thenByExpression)
        {
            ThenByExpressions.Add(thenByExpression);
        }

        protected virtual void AddThenByDescending(Expression<Func<T, object>> thenByDescExpression)
        {
            ThenByDescendingExpressions.Add(thenByDescExpression);
        }

        protected virtual void ApplyPaging(int skip, int take)
        {
            Skip = skip;
            Take = take;
            IsPagingEnabled = true;
        }
    }
}