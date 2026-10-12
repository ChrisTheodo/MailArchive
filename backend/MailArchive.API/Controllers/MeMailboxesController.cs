using System.Security.Claims;
using MailArchive.Application.Abstractions;
using MailArchive.Application.Audit;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Mailboxes;
using MailArchive.Application.Contracts.Me;
using MailArchive.Application.Mailboxes;
using MailArchive.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me/mailboxes")]
public class MeMailboxesController : ControllerBase
{
    private readonly IMailArchiveDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly IMailboxService _mailboxService;

    public MeMailboxesController(
        IMailArchiveDbContext db,
        IAuditLogService auditLogService,
        IMailboxService mailboxService)
    {
        _db = db;
        _auditLogService = auditLogService;
        _mailboxService = mailboxService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyMailboxes()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        var currentUserEmail = User.FindFirstValue(ClaimTypes.Email);
        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);

        var mailboxes = await _db.Mailboxes
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Where(x => x.OwnerUserId == currentUserId)
            .OrderBy(x => x.DisplayName)
            .ToListAsync();

        var mailboxIds = mailboxes
            .Select(x => x.Id)
            .ToList();

        var emailStats = await _db.Emails
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalEmails = x.Count(),
                EmailsWithAttachments = x.Count(email => email.HasAttachments),
                LatestEmailReceivedAt = x.Max(email => (DateTime?)email.ReceivedAt)
            })
            .ToListAsync();

        var attachmentStats = await _db.Attachments
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.Email.MailboxId))
            .GroupBy(x => x.Email.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalAttachments = x.Count()
            })
            .ToListAsync();

        var importStats = await _db.ImportBatches
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.MailboxId))
            .GroupBy(x => x.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalImportBatches = x.Count(),
                PendingImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Pending),
                QueuedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Queued),
                RunningImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Running),
                CompletedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Completed),
                CompletedWithErrorsImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.CompletedWithErrors),
                FailedImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Failed),
                CancelledImports = x.Count(importBatch => importBatch.Status == ImportBatchStatus.Cancelled),
                LatestImportStartedAt = x.Max(importBatch => (DateTime?)importBatch.StartedAt)
            })
            .ToListAsync();

        var importErrorStats = await _db.ImportErrors
            .AsNoTracking()
            .Where(x => mailboxIds.Contains(x.ImportBatch.MailboxId))
            .GroupBy(x => x.ImportBatch.MailboxId)
            .Select(x => new
            {
                MailboxId = x.Key,
                TotalImportErrors = x.Count()
            })
            .ToListAsync();

        var emailStatsByMailboxId = emailStats.ToDictionary(x => x.MailboxId);
        var attachmentStatsByMailboxId = attachmentStats.ToDictionary(x => x.MailboxId);
        var importStatsByMailboxId = importStats.ToDictionary(x => x.MailboxId);
        var importErrorStatsByMailboxId = importErrorStats.ToDictionary(x => x.MailboxId);

        var mailboxResponses = mailboxes
            .Select(mailbox =>
            {
                emailStatsByMailboxId.TryGetValue(mailbox.Id, out var emailStat);
                attachmentStatsByMailboxId.TryGetValue(mailbox.Id, out var attachmentStat);
                importStatsByMailboxId.TryGetValue(mailbox.Id, out var importStat);
                importErrorStatsByMailboxId.TryGetValue(mailbox.Id, out var importErrorStat);

                return new MyMailboxResponse(
                    MailboxId: mailbox.Id,
                    DisplayName: mailbox.DisplayName,
                    OwnerUserId: mailbox.OwnerUserId,
                    OwnerEmail: mailbox.OwnerUser?.Email,
                    TotalEmails: emailStat?.TotalEmails ?? 0,
                    EmailsWithAttachments: emailStat?.EmailsWithAttachments ?? 0,
                    TotalAttachments: attachmentStat?.TotalAttachments ?? 0,
                    TotalImportBatches: importStat?.TotalImportBatches ?? 0,
                    PendingImports: importStat?.PendingImports ?? 0,
                    QueuedImports: importStat?.QueuedImports ?? 0,
                    RunningImports: importStat?.RunningImports ?? 0,
                    CompletedImports: importStat?.CompletedImports ?? 0,
                    CompletedWithErrorsImports: importStat?.CompletedWithErrorsImports ?? 0,
                    FailedImports: importStat?.FailedImports ?? 0,
                    CancelledImports: importStat?.CancelledImports ?? 0,
                    TotalImportErrors: importErrorStat?.TotalImportErrors ?? 0,
                    LatestEmailReceivedAt: emailStat?.LatestEmailReceivedAt,
                    LatestImportStartedAt: importStat?.LatestImportStartedAt
                );
            })
            .ToList();

        var response = new MyMailboxListResponse(
            CurrentUserId: currentUserId,
            CurrentUserEmail: currentUserEmail,
            CurrentUserRole: currentUserRole,
            TotalMailboxes: mailboxResponses.Count,
            Mailboxes: mailboxResponses,
            GeneratedAtUtc: DateTime.UtcNow
        );

        await _auditLogService.LogAsync(
            action: "MyMailboxesViewed",
            entityType: "Mailbox");

        return Ok(ApiResponse<MyMailboxListResponse>.Ok(response));
    }

    [HttpPost]
    public async Task<IActionResult> CreateMyMailbox(MyMailboxCreateRequest request)
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdText, out var currentUserId))
            return Unauthorized(ApiResponse<string>.Fail("CurrentUserNotFound"));

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(ApiResponse<string>.Fail("DisplayNameRequired"));
 
        var result = await _mailboxService.CreateAsync(
            new CreateMailboxRequest(currentUserId, request.DisplayName.Trim()));
 
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));
 
        var x = result.Value!;
 
        await _auditLogService.LogAsync(
            action: "MyMailboxCreated",
            entityType: "Mailbox",
            entityId: x.Id);
 
        var response = new MailboxResponse(
            x.Id,
            x.DisplayName,
            x.OwnerUserId,
            x.OwnerUser?.Email
        );
 
        return Ok(ApiResponse<MailboxResponse>.Ok(response));
    }
}