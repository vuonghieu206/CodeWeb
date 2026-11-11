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
        public DbSet<Category> Categories { get; set; }
        public DbSet<Review> Reviews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Tên danh mục không được trùng
            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();

            // Xóa sách thì xóa luôn đánh giá của sách đó
            modelBuilder.Entity<Review>()
                .HasOne<Book>()
                .WithMany()
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
