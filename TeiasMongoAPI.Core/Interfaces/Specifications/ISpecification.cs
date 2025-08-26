using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace TeiasMongoAPI.Core.Interfaces.Specifications
{
    public interface ISpecification<T>
    {
        Expression<Func<T, bool>>? Criteria { get; }
        List<Expression<Func<T, object>>> Includes { get; }
        Expression<Func<T, object>>? OrderBy { get; }
        Expression<Func<T, object>>? OrderByDescending { get; }
        List<Expression<Func<T, object>>> ThenByExpressions { get; }
        List<Expression<Func<T, object>>> ThenByDescendingExpressions { get; }
        
        int Take { get; }
        int Skip { get; }
        bool IsPagingEnabled { get; }
    }
}