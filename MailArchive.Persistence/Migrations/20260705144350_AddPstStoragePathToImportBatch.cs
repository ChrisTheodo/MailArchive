using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPstStoragePathToImportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBatches_Mailboxes_MailboxId",
                table: "ImportBatches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ImportBatches",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_MailboxId_PstHash",
                table: "ImportBatches");

            migrationBuilder.RenameTable(
                name: "ImportBatches",
                newName: "import_batches");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_Status",
                table: "import_batches",
                newName: "IX_import_batches_Status");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_StartedAt",
                table: "import_batches",
                newName: "IX_import_batches_StartedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_PstHash",
                table: "import_batches",
                newName: "IX_import_batches_PstHash");

            migrationBuilder.RenameIndex(
                name: "IX_ImportBatches_MailboxId",
                table: "import_batches",
                newName: "IX_import_batches_MailboxId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "import_batches",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "PstStoragePath",
                table: "import_batches",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_import_batches",
                table: "import_batches",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_CompletedAt",
                table: "import_batches",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_MailboxId_PstHash",
                table: "import_batches",
                columns: new[] { "MailboxId", "PstHash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails",
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
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_import_batches_Mailboxes_MailboxId",
                table: "import_batches");

            migrationBuilder.DropPrimaryKey(
                name: "PK_import_batches",
                table: "import_batches");

            migrationBuilder.DropIndex(
                name: "IX_import_batches_CompletedAt",
                table: "import_batches");

            migrationBuilder.DropIndex(
                name: "IX_import_batches_MailboxId_PstHash",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "PstStoragePath",
                table: "import_batches");

            migrationBuilder.RenameTable(
                name: "import_batches",
                newName: "ImportBatches");

            migrationBuilder.RenameIndex(
                name: "IX_import_batches_Status",
                table: "ImportBatches",
                newName: "IX_ImportBatches_Status");

            migrationBuilder.RenameIndex(
                name: "IX_import_batches_StartedAt",
                table: "ImportBatches",
                newName: "IX_ImportBatches_StartedAt");

            migrationBuilder.RenameIndex(
                name: "IX_import_batches_PstHash",
                table: "ImportBatches",
                newName: "IX_ImportBatches_PstHash");

            migrationBuilder.RenameIndex(
                name: "IX_import_batches_MailboxId",
                table: "ImportBatches",
                newName: "IX_ImportBatches_MailboxId");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ImportBatches",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ImportBatches",
                table: "ImportBatches",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_MailboxId_PstHash",
                table: "ImportBatches",
                columns: new[] { "MailboxId", "PstHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBatches_Mailboxes_MailboxId",
                table: "ImportBatches",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
