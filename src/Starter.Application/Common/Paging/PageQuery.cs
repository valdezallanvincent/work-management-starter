using System.ComponentModel.DataAnnotations;

namespace Starter.Application.Common.Paging;

public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// Base for list queries. Page size is capped so a client cannot request the whole table.
/// </summary>
public abstract record PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    [StringLength(50)]
    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    [StringLength(100)]
    public string? Search { get; init; }

    public int Skip => (Page - 1) * PageSize;
}
