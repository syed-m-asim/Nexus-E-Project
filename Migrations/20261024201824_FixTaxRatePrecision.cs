using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexusCommunication.Migrations
{
    /// <inheritdoc />
    public partial class FixTaxRatePrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bills.TaxRate must keep 4 decimals (0.1224 = 12.24%); decimal(18,2) rounded it to 0.12.
            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "Bills",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // Existing bills were saved with the rounded rate; set the correct one.
            migrationBuilder.Sql("UPDATE Bills SET TaxRate = 0.1224");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "Bills",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}