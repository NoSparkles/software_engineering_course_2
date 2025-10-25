using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class SplitInvitationToGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationToGame");

            migrationBuilder.CreateTable(
                name: "FromInvitationToGame",
                columns: table => new
                {
                    RoomKey = table.Column<string>(type: "TEXT", nullable: false),
                    ToUsername = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FromInvitationToGame", x => x.RoomKey);
                    table.ForeignKey(
                        name: "FK_FromInvitationToGame_Users_Username",
                        column: x => x.Username,
                        principalTable: "Users",
                        principalColumn: "Username");
                });

            migrationBuilder.CreateTable(
                name: "ToInvitationToGame",
                columns: table => new
                {
                    RoomKey = table.Column<string>(type: "TEXT", nullable: false),
                    FromUsername = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToInvitationToGame", x => x.RoomKey);
                    table.ForeignKey(
                        name: "FK_ToInvitationToGame_Users_Username",
                        column: x => x.Username,
                        principalTable: "Users",
                        principalColumn: "Username");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FromInvitationToGame_Username",
                table: "FromInvitationToGame",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ToInvitationToGame_Username",
                table: "ToInvitationToGame",
                column: "Username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FromInvitationToGame");

            migrationBuilder.DropTable(
                name: "ToInvitationToGame");

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
    }
}
