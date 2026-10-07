using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailFullTextSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Emails_EmailId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailRecipients_Emails_EmailId",
                table: "EmailRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Emails",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MailboxId_InternetMessageId",
                table: "Emails");

            migrationBuilder.RenameTable(
                name: "Emails",
                newName: "emails");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_Subject",
                table: "emails",
                newName: "IX_emails_Subject");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_SentAt",
                table: "emails",
                newName: "IX_emails_SentAt");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_SenderEmail",
                table: "emails",
                newName: "IX_emails_SenderEmail");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_ReceivedAt",
                table: "emails",
                newName: "IX_emails_ReceivedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_MessageHash",
                table: "emails",
                newName: "IX_emails_MessageHash");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_MailboxId_ReceivedAt",
                table: "emails",
                newName: "IX_emails_MailboxId_ReceivedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_MailboxId_MessageHash",
                table: "emails",
                newName: "IX_emails_MailboxId_MessageHash");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_MailboxId",
                table: "emails",
                newName: "IX_emails_MailboxId");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_InternetMessageId",
                table: "emails",
                newName: "IX_emails_InternetMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_Emails_ImportBatchId",
                table: "emails",
                newName: "IX_emails_ImportBatchId");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "emails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "emails",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "emails",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FolderPath",
                table: "emails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_emails",
                table: "emails",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_emails_Subject_BodyText_BodyHtml_SenderEmail_SenderName_Int~",
                table: "emails",
                columns: new[] { "Subject", "BodyText", "BodyHtml", "SenderEmail", "SenderName", "InternetMessageId", "FolderPath" })
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:TsVectorConfig", "english");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_emails_EmailId",
                table: "Attachments",
                column: "EmailId",
                principalTable: "emails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailRecipients_emails_EmailId",
                table: "EmailRecipients",
                column: "EmailId",
                principalTable: "emails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_emails_EmailId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailRecipients_emails_EmailId",
                table: "EmailRecipients");

            migrationBuilder.DropForeignKey(
                name: "FK_emails_Mailboxes_MailboxId",
                table: "emails");

            migrationBuilder.DropForeignKey(
                name: "FK_emails_import_batches_ImportBatchId",
                table: "emails");

            migrationBuilder.DropPrimaryKey(
                name: "PK_emails",
                table: "emails");

            migrationBuilder.DropIndex(
                name: "IX_emails_Subject_BodyText_BodyHtml_SenderEmail_SenderName_Int~",
                table: "emails");

            migrationBuilder.RenameTable(
                name: "emails",
                newName: "Emails");

            migrationBuilder.RenameIndex(
                name: "IX_emails_Subject",
                table: "Emails",
                newName: "IX_Emails_Subject");

            migrationBuilder.RenameIndex(
                name: "IX_emails_SentAt",
                table: "Emails",
                newName: "IX_Emails_SentAt");

            migrationBuilder.RenameIndex(
                name: "IX_emails_SenderEmail",
                table: "Emails",
                newName: "IX_Emails_SenderEmail");

            migrationBuilder.RenameIndex(
                name: "IX_emails_ReceivedAt",
                table: "Emails",
                newName: "IX_Emails_ReceivedAt");

            migrationBuilder.RenameIndex(
                name: "IX_emails_MessageHash",
                table: "Emails",
                newName: "IX_Emails_MessageHash");

            migrationBuilder.RenameIndex(
                name: "IX_emails_MailboxId_ReceivedAt",
                table: "Emails",
                newName: "IX_Emails_MailboxId_ReceivedAt");

            migrationBuilder.RenameIndex(
                name: "IX_emails_MailboxId_MessageHash",
                table: "Emails",
                newName: "IX_Emails_MailboxId_MessageHash");

            migrationBuilder.RenameIndex(
                name: "IX_emails_MailboxId",
                table: "Emails",
                newName: "IX_Emails_MailboxId");

            migrationBuilder.RenameIndex(
                name: "IX_emails_InternetMessageId",
                table: "Emails",
                newName: "IX_Emails_InternetMessageId");

            migrationBuilder.RenameIndex(
                name: "IX_emails_ImportBatchId",
                table: "Emails",
                newName: "IX_Emails_ImportBatchId");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "Emails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "Emails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "Emails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AlterColumn<string>(
                name: "FolderPath",
                table: "Emails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Emails",
                table: "Emails",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MailboxId_InternetMessageId",
                table: "Emails",
                columns: new[] { "MailboxId", "InternetMessageId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Emails_EmailId",
                table: "Attachments",
                column: "EmailId",
                principalTable: "Emails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailRecipients_Emails_EmailId",
                table: "EmailRecipients",
                column: "EmailId",
                principalTable: "Emails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
