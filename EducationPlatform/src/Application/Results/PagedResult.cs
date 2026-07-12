using System;
using System.Collections.Generic;

namespace Application.Results
{
    public class PagedResult<T>
    {
        public IReadOnlyCollection<T> Items { get; init; } = new List<T>().AsReadOnly();
        public int PageIndex { get; init; }
        public int PageSize { get; init; }
        public int TotalItems { get; init; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }
}
