using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NexusCommunication.Data;

#nullable disable

namespace NexusCommunication.Migrations
{
    /// <summary>Adds the FullName column to the AspNetUsers table (ApplicationUser.FullName).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261018120000_AddFullName")]
    public partial class AddFullName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");
        }
    }
}
