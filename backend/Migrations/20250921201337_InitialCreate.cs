using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Friends = table.Column<string>(type: "TEXT", nullable: false),
                    RockPaperScissorsMMR = table.Column<int>(type: "INTEGER", nullable: false),
                    FourInARowMMR = table.Column<int>(type: "INTEGER", nullable: false),
                    PairMatchingMMR = table.Column<int>(type: "INTEGER", nullable: false),
                    TournamentMMR = table.Column<int>(type: "INTEGER", nullable: false),
                    RockPaperScissorsWinStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    FourInARowWinStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    PairMatchingWinStreak = table.Column<int>(type: "INTEGER", nullable: false),
                    TournamentWinStreak = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Username);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
