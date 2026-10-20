using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailboxCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails");

            migrationBuilder.DropForeignKey(
                name: "FK_import_batches_Mailboxes_MailboxId",
                table: "import_batches");

            migrationBuilder.AddForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_import_batches_Mailboxes_MailboxId",
                table: "import_batches",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails");

            migrationBuilder.DropForeignKey(
                name: "FK_import_batches_Mailboxes_MailboxId",
                table: "import_batches");

            migrationBuilder.AddForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_import_batches_Mailboxes_MailboxId",
                table: "import_batches",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
