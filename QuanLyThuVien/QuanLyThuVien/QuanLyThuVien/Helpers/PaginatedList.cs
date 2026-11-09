using Microsoft.EntityFrameworkCore;

namespace QuanLyThuVien.Helpers
{
    public interface IPagedList
    {
        int PageIndex { get; }
        int TotalPages { get; }
        int TotalItems { get; }
        bool HasPrevious { get; }
        bool HasNext { get; }
    }

    public class PaginatedList<T> : IPagedList
    {
        public List<T> Items { get; }
        public int PageIndex { get; }
        public int TotalPages { get; }
        public int TotalItems { get; }
        public int PageSize { get; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            Items = items;
            TotalItems = count;
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        }

        public bool HasPrevious => PageIndex > 1;
        public bool HasNext => PageIndex < TotalPages;

        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;
            var count = await source.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
            if (pageIndex < 1) pageIndex = 1;
            if (pageIndex > totalPages) pageIndex = totalPages;
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}
