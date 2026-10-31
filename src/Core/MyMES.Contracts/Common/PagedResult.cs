namespace MyMES.Contracts.Common;

/// <summary>A page of results together with the information needed to request the next one.</summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}

/// <summary>Factory helpers for <see cref="PagedResult{T}"/>, kept off the generic type itself.</summary>
public static class PagedResult
{
    public static PagedResult<T> Empty<T>(int pageSize) => new([], 1, pageSize, 0);
}

/// <summary>Paging parameters accepted by list endpoints.</summary>
public sealed record PageRequest
{
    private const int MaxPageSize = 200;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    /// <summary>Clamps caller-supplied values so a bad request cannot ask for the whole table.</summary>
    public PageRequest Normalised() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize switch
        {
            < 1 => 25,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        }
    };
}

