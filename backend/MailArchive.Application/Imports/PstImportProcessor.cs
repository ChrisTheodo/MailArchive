using System.Security.Cryptography;
using System.Text;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Common;
using MailArchive.Application.Imports.Parsing;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Application.Imports;

public class PstImportProcessor : IPstImportProcessor
{
    private readonly IMailArchiveDbContext _db;
    private readonly IStoragePathResolver _storagePathResolver;
    private readonly IAttachmentStorageService _attachmentStorageService;
    private readonly IPstParser _pstParser;

    public PstImportProcessor(
        IMailArchiveDbContext db,
        IStoragePathResolver storagePathResolver,
        IAttachmentStorageService attachmentStorageService,
        IPstParser pstParser)
    {
        _db = db;
        _storagePathResolver = storagePathResolver;
        _attachmentStorageService = attachmentStorageService;
        _pstParser = pstParser;
    }

    public async Task<Result<ImportBatch>> ProcessAsync(Guid importBatchId)
    {
        var importBatch = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == importBatchId);

        if (importBatch == null)
            return Result<ImportBatch>.Failure("ImportBatchNotFound");

        if (importBatch.Status == ImportBatchStatus.Completed ||
            importBatch.Status == ImportBatchStatus.CompletedWithErrors)
        {
            return Result<ImportBatch>.Failure("ImportBatchAlreadyCompleted");
        }

        if (importBatch.Status == ImportBatchStatus.Failed)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyFailed");

        if (importBatch.Status == ImportBatchStatus.Cancelled)
            return Result<ImportBatch>.Failure("ImportBatchAlreadyCancelled");

        if (string.IsNullOrWhiteSpace(importBatch.PstStoragePath))
            return await FailProcessingAsync(importBatch, "PstStoragePathMissing");

        var pstFilePath = _storagePathResolver.ResolvePath(importBatch.PstStoragePath);

        if (!File.Exists(pstFilePath))
            return await FailProcessingAsync(importBatch, "PstFileNotFound");

        importBatch.Status = ImportBatchStatus.Running;
        importBatch.StartedAt = DateTime.UtcNow;
        importBatch.CompletedAt = null;

        await _db.SaveChangesAsync();

        IReadOnlyCollection<ParsedPstEmail> parsedEmails;

        try
        {
            parsedEmails = await _pstParser.ParseAsync(pstFilePath);
        }
        catch (Exception ex)
        {
            return await FailProcessingAsync(
                importBatch,
                $"PstParsingFailed: {ex.Message}");
        }

        var importedMessages = 0;
        var failedMessages = 0;
        var index = 0;

        foreach (var parsedEmail in parsedEmails)
        {
            index++;

            var currentStatus = await _db.ImportBatches
                .AsNoTracking()
                .Where(x => x.Id == importBatch.Id)
                .Select(x => x.Status)
                .FirstAsync();

            if (currentStatus == ImportBatchStatus.Cancelled)
            {
                return await CompleteCancellationAsync(
                    importBatch,
                    parsedEmails.Count,
                    importedMessages,
                    failedMessages);
            }

            try
            {
                var email = await MapParsedEmailToEntityAsync(
                    importBatch,
                    parsedEmail,
                    index);

                var exists = await _db.Emails.AnyAsync(x =>
                    x.MailboxId == email.MailboxId &&
                    x.MessageHash == email.MessageHash);

                if (exists)
                    continue;

                _db.Emails.Add(email);
                importedMessages++;
            }
            catch (Exception ex)
            {
                failedMessages++;

                _db.ImportErrors.Add(new ImportError
                {
                    Id = Guid.NewGuid(),
                    ImportBatchId = importBatch.Id,
                    Message = $"Email #{index} failed: {ex.Message}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        importBatch.TotalMessages = parsedEmails.Count;
        importBatch.ImportedMessages = importedMessages;
        importBatch.FailedMessages = failedMessages;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.Status = failedMessages > 0
            ? ImportBatchStatus.CompletedWithErrors
            : ImportBatchStatus.Completed;

        await _db.SaveChangesAsync();

        var completedImport = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstAsync(x => x.Id == importBatch.Id);

        return Result<ImportBatch>.Success(completedImport);
    }

    private async Task<Email> MapParsedEmailToEntityAsync(
        ImportBatch importBatch,
        ParsedPstEmail parsedEmail,
        int index)
    {
        if (string.IsNullOrWhiteSpace(parsedEmail.SenderEmail))
            throw new InvalidOperationException("ParsedEmailSenderEmailRequired");

        var now = DateTime.UtcNow;
        var emailId = Guid.NewGuid();

        var messageHash = CreateMessageHash(
            importBatch.Id,
            index,
            parsedEmail.InternetMessageId,
            parsedEmail.SenderEmail,
            parsedEmail.Subject,
            parsedEmail.SentAt);

        var recipients = parsedEmail.Recipients
            .Where(x => !string.IsNullOrWhiteSpace(x.RecipientEmail))
            .Select(x => new EmailRecipient
            {
                Id = Guid.NewGuid(),
                EmailId = emailId,
                RecipientType = x.RecipientType,
                RecipientEmail = x.RecipientEmail.Trim().ToLowerInvariant(),
                RecipientName = string.IsNullOrWhiteSpace(x.RecipientName)
                    ? null
                    : x.RecipientName.Trim()
            })
            .ToList();

        if (recipients.Count == 0)
        {
            recipients.Add(new EmailRecipient
            {
                Id = Guid.NewGuid(),
                EmailId = emailId,
                RecipientType = RecipientType.To,
                RecipientEmail = importBatch.Mailbox.OwnerUser.Email,
                RecipientName = importBatch.Mailbox.OwnerUser.DisplayName
            });
        }

        var attachments = new List<Attachment>();

        foreach (var parsedAttachment in parsedEmail.Attachments)
        {
            if (parsedAttachment.Content.Length == 0)
                continue;

            var storedAttachment = await _attachmentStorageService.SaveAsync(
                parsedAttachment.FileName,
                parsedAttachment.ContentType,
                parsedAttachment.Content);

            attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                EmailId = emailId,
                FileName = storedAttachment.FileName,
                ContentType = storedAttachment.ContentType,
                SizeBytes = storedAttachment.SizeBytes,
                StoragePath = storedAttachment.StoragePath,
                ContentHash = storedAttachment.ContentHash
            });
        }

        return new Email
        {
            Id = emailId,
            MailboxId = importBatch.MailboxId,
            ImportBatchId = importBatch.Id,
            InternetMessageId = parsedEmail.InternetMessageId,
            MessageHash = messageHash,
            FolderPath = string.IsNullOrWhiteSpace(parsedEmail.FolderPath)
                ? "Inbox"
                : parsedEmail.FolderPath.Trim(),
            SenderEmail = parsedEmail.SenderEmail.Trim().ToLowerInvariant(),
            SenderName = string.IsNullOrWhiteSpace(parsedEmail.SenderName)
                ? null
                : parsedEmail.SenderName.Trim(),
            Subject = string.IsNullOrWhiteSpace(parsedEmail.Subject)
                ? "(No subject)"
                : parsedEmail.Subject.Trim(),
            BodyText = parsedEmail.BodyText,
            BodyHtml = parsedEmail.BodyHtml,
            SentAt = parsedEmail.SentAt,
            ReceivedAt = parsedEmail.ReceivedAt ?? parsedEmail.SentAt ?? now,
            HasAttachments = attachments.Count > 0,
            CreatedAt = now,
            Recipients = recipients,
            Attachments = attachments
        };
    }

    private async Task<Result<ImportBatch>> CompleteCancellationAsync(
        ImportBatch importBatch,
        int totalMessages,
        int importedMessages,
        int failedMessages)
    {
        importBatch.Status = ImportBatchStatus.Cancelled;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.TotalMessages = totalMessages;
        importBatch.ImportedMessages = importedMessages;
        importBatch.FailedMessages = failedMessages;

        await _db.SaveChangesAsync();

        var cancelledImport = await _db.ImportBatches
            .Include(x => x.Mailbox)
            .ThenInclude(x => x.OwnerUser)
            .FirstAsync(x => x.Id == importBatch.Id);

        return Result<ImportBatch>.Success(cancelledImport);
    }

    private async Task<Result<ImportBatch>> FailProcessingAsync(
        ImportBatch importBatch,
        string errorMessage)
    {
        importBatch.Status = ImportBatchStatus.Failed;
        importBatch.CompletedAt = DateTime.UtcNow;
        importBatch.TotalMessages = Math.Max(importBatch.TotalMessages, 0);
        importBatch.ImportedMessages = Math.Max(importBatch.ImportedMessages, 0);
        importBatch.FailedMessages = Math.Max(importBatch.FailedMessages, 1);

        _db.ImportErrors.Add(new ImportError
        {
            Id = Guid.NewGuid(),
            ImportBatchId = importBatch.Id,
            Message = errorMessage,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        return Result<ImportBatch>.Failure(errorMessage);
    }

    private static string CreateMessageHash(
        Guid importBatchId,
        int index,
        string? internetMessageId,
        string senderEmail,
        string? subject,
        DateTime? sentAt)
    {
        var raw = string.Join("|",
            importBatchId,
            index,
            internetMessageId ?? string.Empty,
            senderEmail,
            subject ?? string.Empty,
            sentAt?.ToString("O") ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}