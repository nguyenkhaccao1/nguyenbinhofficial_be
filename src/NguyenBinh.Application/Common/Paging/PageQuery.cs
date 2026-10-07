using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NguyenBinh.Shared.Results;

namespace NguyenBinh.Application.Common.Paging;

/// <summary>Tham so danh sach chung: ?q=&amp;page=&amp;pageSize=&amp;sort=-updatedAt,name</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Sort { get; set; }

    public int SafePage => Math.Max(1, Page);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
    public string? Search => string.IsNullOrWhiteSpace(Q) ? null : Q.Trim();
}

/// <summary>Map ten cot sort (tu client) → bieu thuc. Ten khong co trong map bi bo qua (chong sort tuy y).</summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, LambdaExpression> _columns = new(StringComparer.OrdinalIgnoreCase);

    public SortMap<T> Add<TKey>(string name, Expression<Func<T, TKey>> selector)
    {
        _columns[name] = selector;
        return this;
    }

    public IQueryable<T> Apply(IQueryable<T> query, string? sort, string defaultSort)
    {
        var applied = ApplyCore(query, sort);
        return applied ?? ApplyCore(query, defaultSort) ?? query;
    }

    private IOrderedQueryable<T>? ApplyCore(IQueryable<T> query, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return null;

        IOrderedQueryable<T>? ordered = null;
        foreach (var raw in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var desc = raw.StartsWith('-');
            var name = raw.TrimStart('-', '+');
            if (!_columns.TryGetValue(name, out var selector)) continue;

            var method = (ordered is null, desc) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending),
            };

            var source = (IQueryable<T>?)ordered ?? query;
            var call = Expression.Call(typeof(Queryable), method,
                [typeof(T), selector.ReturnType], source.Expression, Expression.Quote(selector));
            ordered = (IOrderedQueryable<T>)source.Provider.CreateQuery<T>(call);
        }

        return ordered;
    }
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery page,
        CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        return PagedResult<T>.Create(items, page.SafePage, page.SafePageSize, total);
    }
}
