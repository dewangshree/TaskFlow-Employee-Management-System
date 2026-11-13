using Microsoft.EntityFrameworkCore;

namespace TaskFlow.Application.Common;

public enum SortDirection { Asc, Desc }

/// <summary>Base query parameters for every server-side paginated list.</summary>
public abstract record PagedQuery
{
    public const int MaxPageSize = 100;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; } = SortDirection.Desc;

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public static class QueryableExtensions
{
    /// <summary>
    /// Executes COUNT and a LIMIT/OFFSET page query on the server. Only the
    /// requested page is materialised, never the whole table.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = total == 0
            ? []
            : await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, total);
    }

    public static IOrderedQueryable<TSource> OrderByDirection<TSource, TKey>(
        this IQueryable<TSource> source,
        System.Linq.Expressions.Expression<Func<TSource, TKey>> key,
        SortDirection direction) =>
        direction == SortDirection.Asc ? source.OrderBy(key) : source.OrderByDescending(key);
}
