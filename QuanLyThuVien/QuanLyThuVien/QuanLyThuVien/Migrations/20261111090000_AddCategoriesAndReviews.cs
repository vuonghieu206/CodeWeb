
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyThuVien.Migrations
{
    public partial class AddCategoriesAndReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Categories đã tồn tại, chỉ bổ sung Description
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Categories",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            // Tạo index duy nhất cho tên danh mục nếu chưa có
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Categories_Name'
                    AND object_id = OBJECT_ID('dbo.Categories')
                )
                BEGIN
                    CREATE UNIQUE INDEX IX_Categories_Name
                    ON dbo.Categories(Name);
                END
            ");

            // Tạo bảng Reviews
            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(
                        type: "int",
                        nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    BookId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    AccountId = table.Column<int>(
                        type: "int",
                        nullable: false),

                    UserName = table.Column<string>(
                        type: "nvarchar(100)",
                        maxLength: 100,
                        nullable: false),

                    Rating = table.Column<int>(
                        type: "int",
                        nullable: false),

                    Comment = table.Column<string>(
                        type: "nvarchar(1000)",
                        maxLength: 1000,
                        nullable: true),

                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);

                    table.ForeignKey(
                        name: "FK_Reviews_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookId",
                table: "Reviews",
                column: "BookId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Chỉ xóa bảng Reviews do migration này tạo
            migrationBuilder.DropTable(
                name: "Reviews");

            // Không xóa bảng Categories
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Categories_Name'
                    AND object_id = OBJECT_ID('dbo.Categories')
                )
                DROP INDEX IX_Categories_Name ON dbo.Categories;
            ");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Categories");
        }
    }
}
