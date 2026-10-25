using System.Linq.Expressions;
using BabysittingPool.Application;
using Microsoft.EntityFrameworkCore;

namespace BabysittingPool.Infrastructure;

internal static class KeysetQuery
{
    /// <summary>
    /// Orders by (<paramref name="at"/>, <paramref name="id"/>), starts after <paramref name="page"/>'s cursor and
    /// fetches its over-fetched row count. Descending is newest first.
    /// </summary>
    public static async Task<List<T>> ToKeysetPageAsync<T>(
        this IQueryable<T> source,
        PageRequest page,
        Expression<Func<T, DateTime>> at,
        Expression<Func<T, Guid>> id,
        bool descending,
        CancellationToken cancellationToken)
    {
        if (page.After is { } position)
        {
            source = source.Where(Past(position, at, id, descending));
        }

        var ordered = descending
            ? source.OrderByDescending(at).ThenByDescending(id)
            : source.OrderBy(at).ThenBy(id);
        return await ordered.Take(page.Take).ToListAsync(cancellationToken);
    }

    // at <> p.At || (at == p.At && id <> p.Id), with <> being < when descending and > when ascending.
    private static Expression<Func<T, bool>> Past<T>(
        KeysetPosition position, Expression<Func<T, DateTime>> at, Expression<Func<T, Guid>> id, bool descending)
    {
        var row = Expression.Parameter(typeof(T), "r");
        var rowAt = new Rebind(at.Parameters[0], row).Visit(at.Body)!;
        var rowId = new Rebind(id.Parameters[0], row).Visit(id.Body)!;
        var positionAt = Expression.Constant(position.At);
        var idOrder = Expression.Call(rowId, nameof(Guid.CompareTo), null, Expression.Constant(position.Id));
        var zero = Expression.Constant(0);

        var atBefore = descending ? Expression.LessThan(rowAt, positionAt) : Expression.GreaterThan(rowAt, positionAt);
        var idBefore = descending ? Expression.LessThan(idOrder, zero) : Expression.GreaterThan(idOrder, zero);
        var body = Expression.OrElse(atBefore, Expression.AndAlso(Expression.Equal(rowAt, positionAt), idBefore));
        return Expression.Lambda<Func<T, bool>>(body, row);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
