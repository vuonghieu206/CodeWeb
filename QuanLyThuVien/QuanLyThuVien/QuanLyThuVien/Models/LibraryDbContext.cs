using Microsoft.EntityFrameworkCore;

namespace QuanLyThuVien.Models
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) { }

        public DbSet<BorrowRecord> BorrowRecords { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Reader> Readers { get; set; }
        public DbSet<Penalty> Penalties { get; set; }
        public DbSet<Account> Accounts { get; set; }
    }
}