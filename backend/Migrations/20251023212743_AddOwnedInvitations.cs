using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnedInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FromInvitationsToGame");

            migrationBuilder.DropTable(
                name: "ToInvitationsToGame");

            migrationBuilder.AddColumn<string>(
                name: "IncomingInviteToGameRequests",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutcomingInviteToGameRequests",
                table: "Users",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncomingInviteToGameRequests",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OutcomingInviteToGameRequests",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "FromInvitationsToGame",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoomKey = table.Column<string>(type: "TEXT", nullable: false),
                    ToUsername = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FromInvitationsToGame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FromInvitationsToGame_Users_Username",
                        column: x => x.Username,
                        principalTable: "Users",
                        principalColumn: "Username",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToInvitationsToGame",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FromUsername = table.Column<string>(type: "TEXT", nullable: false),
                    RoomKey = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToInvitationsToGame", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToInvitationsToGame_Users_Username",
                        column: x => x.Username,
                        principalTable: "Users",
                        principalColumn: "Username",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FromInvitationsToGame_Username",
                table: "FromInvitationsToGame",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_ToInvitationsToGame_Username",
                table: "ToInvitationsToGame",
                column: "Username");
        }
    }
}
