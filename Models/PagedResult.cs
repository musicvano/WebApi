namespace WebApi.Models
{
    public class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; set; }

        public int Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
