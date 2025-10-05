using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserFriendRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TournamentMMR",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TournamentWinStreak",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "IncomingFriendRequests",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OutgoingFriendRequests",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncomingFriendRequests",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OutgoingFriendRequests",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "TournamentMMR",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TournamentWinStreak",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
