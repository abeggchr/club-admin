using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class SmtpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SmtpFromAddress",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPassword",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "Settings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpSecurity",
                table: "Settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUsername",
                table: "Settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SmtpFromAddress",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SmtpPassword",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SmtpSecurity",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "SmtpUsername",
                table: "Settings");
        }
    }
}
