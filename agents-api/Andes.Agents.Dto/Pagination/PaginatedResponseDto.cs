namespace Andes.Agents.Dto.Pagination;

/// <summary>One page of a collection.</summary>
/// <typeparam name="T">The item type.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Skip">Number of items skipped before this page.</param>
/// <param name="Take">Page size requested.</param>
/// <param name="TotalCount">Number of items in the whole collection.</param>
public sealed record PaginatedResponseDto<T>(IReadOnlyList<T> Items, int Skip, int Take, int TotalCount);
