using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messenger.Migrations
{
    /// <inheritdoc />
    public partial class AddModeratedMessagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModeratedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlainText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModeratedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModeratedMessages_Chats_ChatId",
                        column: x => x.ChatId,
                        principalTable: "Chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModeratedMessages_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModeratedMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedMessages_ChatId",
                table: "ModeratedMessages",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedMessages_CreatedAt",
                table: "ModeratedMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedMessages_MessageId",
                table: "ModeratedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ModeratedMessages_UserId",
                table: "ModeratedMessages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModeratedMessages");
        }
    }
}
