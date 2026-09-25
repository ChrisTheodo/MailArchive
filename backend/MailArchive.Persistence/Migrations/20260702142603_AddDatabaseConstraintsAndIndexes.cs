using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseConstraintsAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Mailboxes_Users_OwnerUserId",
                table: "Mailboxes");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Mailboxes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PstHash",
                table: "ImportBatches",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PstFilename",
                table: "ImportBatches",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "Emails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "Emails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "Emails",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MessageHash",
                table: "Emails",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "InternetMessageId",
                table: "Emails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FolderPath",
                table: "Emails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientName",
                table: "EmailRecipients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "EmailRecipients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "StoragePath",
                table: "Attachments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Attachments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "Attachments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                table: "Attachments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mailboxes_OwnerUserId_DisplayName",
                table: "Mailboxes",
                columns: new[] { "OwnerUserId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_MailboxId_PstHash",
                table: "ImportBatches",
                columns: new[] { "MailboxId", "PstHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_PstHash",
                table: "ImportBatches",
                column: "PstHash");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_StartedAt",
                table: "ImportBatches",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_Status",
                table: "ImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MailboxId_InternetMessageId",
                table: "Emails",
                columns: new[] { "MailboxId", "InternetMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MailboxId_MessageHash",
                table: "Emails",
                columns: new[] { "MailboxId", "MessageHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MailboxId_ReceivedAt",
                table: "Emails",
                columns: new[] { "MailboxId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Emails_MessageHash",
                table: "Emails",
                column: "MessageHash");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_SentAt",
                table: "Emails",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_Emails_Subject",
                table: "Emails",
                column: "Subject");

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_EmailId_RecipientType",
                table: "EmailRecipients",
                columns: new[] { "EmailId", "RecipientType" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailRecipients_RecipientEmail_RecipientType",
                table: "EmailRecipients",
                columns: new[] { "RecipientEmail", "RecipientType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId",
                table: "AuditLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ContentHash",
                table: "Attachments",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_EmailId_FileName",
                table: "Attachments",
                columns: new[] { "EmailId", "FileName" });

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Mailboxes_Users_OwnerUserId",
                table: "Mailboxes",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails");

            migrationBuilder.DropForeignKey(
                name: "FK_Mailboxes_Users_OwnerUserId",
                table: "Mailboxes");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Mailboxes_OwnerUserId_DisplayName",
                table: "Mailboxes");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_MailboxId_PstHash",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_PstHash",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_StartedAt",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_Status",
                table: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MailboxId_InternetMessageId",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MailboxId_MessageHash",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MailboxId_ReceivedAt",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_MessageHash",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_SentAt",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_Emails_Subject",
                table: "Emails");

            migrationBuilder.DropIndex(
                name: "IX_EmailRecipients_EmailId_RecipientType",
                table: "EmailRecipients");

            migrationBuilder.DropIndex(
                name: "IX_EmailRecipients_RecipientEmail_RecipientType",
                table: "EmailRecipients");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ContentHash",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_EmailId_FileName",
                table: "Attachments");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Users",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Mailboxes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "PstHash",
                table: "ImportBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "PstFilename",
                table: "ImportBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Subject",
                table: "Emails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderName",
                table: "Emails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SenderEmail",
                table: "Emails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MessageHash",
                table: "Emails",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "InternetMessageId",
                table: "Emails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FolderPath",
                table: "Emails",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientName",
                table: "EmailRecipients",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RecipientEmail",
                table: "EmailRecipients",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "StoragePath",
                table: "Attachments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Attachments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "Attachments",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                table: "Attachments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_ImportBatches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_Mailboxes_MailboxId",
                table: "Emails",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mailboxes_Users_OwnerUserId",
                table: "Mailboxes",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
