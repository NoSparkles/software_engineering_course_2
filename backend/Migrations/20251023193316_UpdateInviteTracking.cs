using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateInviteTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitationToGame",
                columns: table => new
                {
                    RoomKey = table.Column<string>(type: "TEXT", nullable: false),
                    FromUsername = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Username1 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationToGame", x => x.RoomKey);
                    table.ForeignKey(
                        name: "FK_InvitationToGame_Users_Username",
                        column: x => x.Username,
                        principalTable: "Users",
                        principalColumn: "Username");
                    table.ForeignKey(
                        name: "FK_InvitationToGame_Users_Username1",
                        column: x => x.Username1,
                        principalTable: "Users",
                        principalColumn: "Username");
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitationToGame_Username",
                table: "InvitationToGame",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationToGame_Username1",
                table: "InvitationToGame",
                column: "Username1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationToGame");
        }
    }
}
