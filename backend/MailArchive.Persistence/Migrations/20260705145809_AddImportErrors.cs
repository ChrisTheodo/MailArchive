using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchive.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails");

            migrationBuilder.CreateTable(
                name: "import_errors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_errors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_errors_import_batches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_errors_CreatedAt",
                table: "import_errors",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_import_errors_ImportBatchId",
                table: "import_errors",
                column: "ImportBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails");

            migrationBuilder.DropTable(
                name: "import_errors");

            migrationBuilder.AddForeignKey(
                name: "FK_Emails_import_batches_ImportBatchId",
                table: "Emails",
                column: "ImportBatchId",
                principalTable: "import_batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
