using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    // Temporary: links each carried-over Bewegung to its Buchung, dropped below.
                    EntryId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.AddColumn<int>(
                name: "TransactionId",
                table: "JournalEntries",
                type: "integer",
                nullable: true);

            // One Bewegung per existing Buchung, with that Buchung's date and amount. Inserted in Id order so
            // that creation order is kept.
            migrationBuilder.Sql(
                """
                INSERT INTO "Transactions" ("Date", "Amount", "EntryId")
                SELECT "Date", "Amount", "Id" FROM "JournalEntries" ORDER BY "Id";
                """);
            migrationBuilder.Sql(
                """
                UPDATE "JournalEntries" AS e SET "TransactionId" = t."Id"
                FROM "Transactions" AS t WHERE t."EntryId" = e."Id";
                """);

            migrationBuilder.DropColumn(
                name: "EntryId",
                table: "Transactions");

            migrationBuilder.AlterColumn<int>(
                name: "TransactionId",
                table: "JournalEntries",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_Date",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "JournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_TransactionId",
                table: "JournalEntries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Transactions_TransactionId",
                table: "JournalEntries",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Transactions_TransactionId",
                table: "JournalEntries");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "JournalEntries",
                type: "date",
                nullable: true);

            // Each Buchung takes back the date of its Bewegung; Bewegungen with several Buchungen are not restorable.
            migrationBuilder.Sql(
                """
                UPDATE "JournalEntries" AS e SET "Date" = t."Date"
                FROM "Transactions" AS t WHERE t."Id" = e."TransactionId";
                """);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "Date",
                table: "JournalEntries",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_TransactionId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "JournalEntries");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_Date",
                table: "JournalEntries",
                column: "Date");
        }
    }
}
