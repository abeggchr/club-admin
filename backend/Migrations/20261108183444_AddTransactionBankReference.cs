using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionBankReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "Transactions",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BankReference",
                table: "Transactions",
                column: "BankReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_BankReference",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "Transactions");
        }
    }
}
