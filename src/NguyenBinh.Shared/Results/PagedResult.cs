namespace NguyenBinh.Shared.Results;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public static PagedResult<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalItems) =>
        new() { Items = items, Page = page, PageSize = pageSize, TotalItems = totalItems };

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        PagedResult<TOut>.Create(Items.Select(map).ToList(), Page, PageSize, TotalItems);
}
