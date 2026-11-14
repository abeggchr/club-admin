using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonPayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayerId",
                table: "Persons",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PayerId",
                table: "Persons",
                column: "PayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Persons_PayerId",
                table: "Persons",
                column: "PayerId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Persons_PayerId",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Persons_PayerId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PayerId",
                table: "Persons");
        }
    }
}
