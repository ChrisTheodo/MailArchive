using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails");

            migrationBuilder.AddForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails");

            migrationBuilder.AddForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
