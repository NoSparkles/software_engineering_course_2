using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FromInvitationToGame_Users_Username",
                table: "FromInvitationToGame");

            migrationBuilder.DropForeignKey(
                name: "FK_ToInvitationToGame_Users_Username",
                table: "ToInvitationToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame");

            migrationBuilder.RenameTable(
                name: "ToInvitationToGame",
                newName: "ToInvitationsToGame");

            migrationBuilder.RenameTable(
                name: "FromInvitationToGame",
                newName: "FromInvitationsToGame");

            migrationBuilder.RenameIndex(
                name: "IX_ToInvitationToGame_Username",
                table: "ToInvitationsToGame",
                newName: "IX_ToInvitationsToGame_Username");

            migrationBuilder.RenameIndex(
                name: "IX_FromInvitationToGame_Username",
                table: "FromInvitationsToGame",
                newName: "IX_FromInvitationsToGame_Username");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToInvitationsToGame",
                table: "ToInvitationsToGame",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FromInvitationsToGame",
                table: "FromInvitationsToGame",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FromInvitationsToGame_Users_Username",
                table: "FromInvitationsToGame",
                column: "Username",
                principalTable: "Users",
                principalColumn: "Username",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ToInvitationsToGame_Users_Username",
                table: "ToInvitationsToGame",
                column: "Username",
                principalTable: "Users",
                principalColumn: "Username",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FromInvitationsToGame_Users_Username",
                table: "FromInvitationsToGame");

            migrationBuilder.DropForeignKey(
                name: "FK_ToInvitationsToGame_Users_Username",
                table: "ToInvitationsToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ToInvitationsToGame",
                table: "ToInvitationsToGame");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FromInvitationsToGame",
                table: "FromInvitationsToGame");

            migrationBuilder.RenameTable(
                name: "ToInvitationsToGame",
                newName: "ToInvitationToGame");

            migrationBuilder.RenameTable(
                name: "FromInvitationsToGame",
                newName: "FromInvitationToGame");

            migrationBuilder.RenameIndex(
                name: "IX_ToInvitationsToGame_Username",
                table: "ToInvitationToGame",
                newName: "IX_ToInvitationToGame_Username");

            migrationBuilder.RenameIndex(
                name: "IX_FromInvitationsToGame_Username",
                table: "FromInvitationToGame",
                newName: "IX_FromInvitationToGame_Username");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToInvitationToGame",
                table: "ToInvitationToGame",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FromInvitationToGame",
                table: "FromInvitationToGame",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FromInvitationToGame_Users_Username",
                table: "FromInvitationToGame",
                column: "Username",
                principalTable: "Users",
                principalColumn: "Username");

            migrationBuilder.AddForeignKey(
                name: "FK_ToInvitationToGame_Users_Username",
                table: "ToInvitationToGame",
                column: "Username",
                principalTable: "Users",
                principalColumn: "Username");
        }
    }
}
