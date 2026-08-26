using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionService.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EventCount",
                table: "Operations",
                newName: "RetryCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "Operations",
                newName: "EventCount");
        }
    }
}
