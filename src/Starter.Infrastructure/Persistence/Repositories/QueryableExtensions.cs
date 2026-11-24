using Microsoft.EntityFrameworkCore;
using Starter.Application.Common.Paging;

namespace Starter.Infrastructure.Persistence.Repositories;

internal static class QueryableExtensions
{
    /// <summary>
    /// Offset pagination: one COUNT and one page query. Simple and supports "jump to page", but cost
    /// grows with the offset; keyset pagination is the upgrade path for very large tables.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = total == 0
            ? []
            : await query.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }

    public static IOrderedQueryable<T> OrderByDirection<T, TKey>(
        this IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, TKey>> keySelector,
        SortDirection direction) =>
        direction == SortDirection.Descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

    public static string? NormalizedSearch(this PageQuery query) =>
        string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
}
