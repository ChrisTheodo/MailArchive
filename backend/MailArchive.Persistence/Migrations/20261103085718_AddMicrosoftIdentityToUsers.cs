using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMicrosoftIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MicrosoftObjectId",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MicrosoftTenantId",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MicrosoftTenantId_MicrosoftObjectId",
                table: "Users",
                columns: new[] { "MicrosoftTenantId", "MicrosoftObjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_MicrosoftTenantId_MicrosoftObjectId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MicrosoftObjectId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MicrosoftTenantId",
                table: "Users");
        }
    }
}
