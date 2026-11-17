using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NexusCommunication.Data;

#nullable disable

namespace NexusCommunication.Migrations
{
    /// <summary>Adds the ImagePath column to the Products table (Product.ImagePath).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261028120000_AddProductImagePath")]
    public partial class AddProductImagePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Products",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Products");
        }
    }
}
