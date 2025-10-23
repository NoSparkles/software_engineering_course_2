using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "ToInvitationToGame",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "FromInvitationToGame",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0)
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ToInvitationToGame");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "FromInvitationToGame");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame",
                column: "RoomKey");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame",
                column: "RoomKey");
        }
    }
}
