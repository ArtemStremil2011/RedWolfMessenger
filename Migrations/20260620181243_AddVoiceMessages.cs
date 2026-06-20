using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messenger.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "FileMessages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "FileMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "file");

            migrationBuilder.CreateIndex(
                name: "IX_FileMessages_MessageType",
                table: "FileMessages",
                column: "MessageType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileMessages_MessageType",
                table: "FileMessages");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "FileMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "FileMessages");
        }
    }
}
