using MailArchive.Domain.Enums;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XstReader;

namespace MailArchive.Application.Imports.Parsing;

/// <summary>
/// Συγκεντρώνει ό,τι παραλείφθηκε ή δεν μπόρεσε να διαβαστεί κατά το parse,
/// ώστε το import να μην χάνει δεδομένα σιωπηλά.
/// </summary>
public sealed class PstParseReport
{
    private const int MaxWarnings = 500;

    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Warnings => _warnings;

    public int FoldersVisited { get; internal set; }
    public int MessagesParsed { get; internal set; }
    public int MessagesSkipped { get; internal set; }
    public int AttachmentsSaved { get; internal set; }
    public int AttachmentsSkipped { get; internal set; }
    public int SendersUnresolved { get; internal set; }
    public int RecipientsWithoutEmail { get; internal set; }

    internal void Warn(string message)
    {
        if (_warnings.Count < MaxWarnings)
            _warnings.Add(message);
    }
}

public class XstReaderPstParser : IPstParser
{
    /// <summary>Μέγιστο μέγεθος συνημμένου που φορτώνεται σε byte[] (το ParsedPstAttachment κρατά bytes).</summary>
    private const long MaxAttachmentBytes = 100L * 1024 * 1024;

    private const int MaxFolderDepth = 256;
    private const int MaxFileNameLength = 120;

    /// <summary>Κωδικοσελίδα όταν ένα σώμα δεν είναι έγκυρο UTF-8 και δεν δηλώνει δική του (1253 = Ελληνικά).</summary>
    private const int FallbackCodePage = 1253;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private static readonly Regex EmailRegex =
        new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex InlineWhitespaceRegex =
        new(@"\s+", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HorizontalWhitespaceRegex =
        new(@"[ \t]+", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex SpacesAroundNewlineRegex =
        new(@" ?\n ?", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex ManyNewlinesRegex =
        new(@"\n{3,}", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex NonAlphanumericRegex =
        new(@"[^a-zA-Z0-9]", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex ScriptRegex =
        new(@"<script\b.*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex StyleRegex =
        new(@"<style\b.*?</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HtmlCommentRegex =
        new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HtmlBreakRegex =
        new(@"<\s*br\s*/?\s*>|</\s*(p|div|tr|li|h[1-6]|table|blockquote)\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HtmlCellRegex =
        new(@"</\s*t[dh]\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HtmlTagRegex =
        new(@"<[^>]+>", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex HtmlMarkerRegex =
        new(@"<\s*(!doctype|html|head|body|div|p|br|span|table|tr|td|a|b|i|u|font|meta|img|ul|ol|li|h[1-6])\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex CharsetRegex =
        new(@"charset\s*=\s*[""']?\s*([\w\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex AnsiCodePageRegex =
        new(@"\\ansicpg(\d+)", RegexOptions.Compiled, RegexTimeout);

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> GetterCache = new();

    static XstReaderPstParser()
    {
        // Απαραίτητο για κωδικοσελίδες όπως windows-1253 (ελληνικά) σε .NET Core / .NET 5+.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    // ------------------------------------------------------------------
    // Δημόσιο API
    // ------------------------------------------------------------------

    /// <summary>
    /// Συμβατό με το IPstParser: συγκεντρώνει όλα τα μηνύματα στη μνήμη.
    /// Για μεγάλα PST προτιμήστε το <see cref="ParseStreamingAsync"/>.
    /// </summary>
    public async Task<IReadOnlyCollection<ParsedPstEmail>> ParseAsync(
        string pstFilePath,
        CancellationToken cancellationToken = default)
    {
        var emails = new List<ParsedPstEmail>();

        await ParseStreamingAsync(
            pstFilePath,
            emails.Add,
            report: null,
            cancellationToken);

        return emails;
    }

    /// <summary>
    /// Διαβάζει το PST και καλεί το <paramref name="onEmail"/> για κάθε μήνυμα,
    /// χωρίς να κρατά όλο το αρχείο στη μνήμη. Τρέχει σε thread του pool.
    /// Σφάλματα του callback ΔΕΝ καταπίνονται (διαφέρουν από σφάλματα parsing).
    /// </summary>
    public Task ParseStreamingAsync(
        string pstFilePath,
        Action<ParsedPstEmail> onEmail,
        PstParseReport? report = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pstFilePath))
            throw new ArgumentException("PST file path is required.", nameof(pstFilePath));

        ArgumentNullException.ThrowIfNull(onEmail);

        if (!File.Exists(pstFilePath))
            throw new FileNotFoundException("PST file was not found.", pstFilePath);

        ValidatePstHeader(pstFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(
            () => ParseCore(pstFilePath, onEmail, report ?? new PstParseReport(), cancellationToken),
            cancellationToken);
    }

    // ------------------------------------------------------------------
    // Πυρήνας parsing
    // ------------------------------------------------------------------

    private sealed class ParseContext
    {
        public ParseContext(
            XstFile xst,
            Action<ParsedPstEmail> onEmail,
            PstParseReport report,
            string tempDirectory,
            CancellationToken cancellationToken)
        {
            Xst = xst;
            OnEmail = onEmail;
            Report = report;
            TempDirectory = tempDirectory;
            CancellationToken = cancellationToken;
        }

        public XstFile Xst { get; }
        public Action<ParsedPstEmail> OnEmail { get; }
        public PstParseReport Report { get; }
        public string TempDirectory { get; }
        public CancellationToken CancellationToken { get; }
        public HashSet<object> VisitedFolders { get; } = new(ReferenceEqualityComparer.Instance);
        public int AttachmentCounter { get; set; }
    }

    private static void ParseCore(
        string pstFilePath,
        Action<ParsedPstEmail> onEmail,
        PstParseReport report,
        CancellationToken cancellationToken)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"mailarchive-pst-{Guid.NewGuid():N}");

        XstFile? xstFile = null;

        try
        {
            xstFile = new XstFile(pstFilePath);

            var context = new ParseContext(
                xstFile,
                onEmail,
                report,
                tempDirectory,
                cancellationToken);

            foreach (var root in ResolveRootFolders(xstFile))
            {
                TraverseFolder(root, string.Empty, 0, context);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"XstReaderParsingFailed: {GetInnermostMessage(ex)}",
                ex);
        }
        finally
        {
            // Απελευθερώνει το file handle του PST.
            (xstFile as IDisposable)?.Dispose();

            TryDeleteDirectory(tempDirectory);
        }
    }

    private static IEnumerable<XstFolder> ResolveRootFolders(XstFile xstFile)
    {
        // Η ReadMessages()/ReadMessageDetails() δεν υπάρχει σε αυτή την έκδοση.
        if (GetObject(xstFile, "RootFolder") is XstFolder rootFolder)
        {
            yield return rootFolder;
            yield break;
        }

        // Άλλες εκδόσεις εκθέτουν το δέντρο μέσω ReadFolderTree().
        var readFolderTree = xstFile.GetType().GetMethod(
            "ReadFolderTree",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (readFolderTree == null)
            throw new InvalidOperationException("XstReader does not expose a readable folder tree.");

        var tree = readFolderTree.Invoke(xstFile, null);

        if (tree is XstFolder treeRoot)
        {
            yield return treeRoot;
            yield break;
        }

        foreach (var child in GetEnumerable(tree, "Folders"))
        {
            if (child is XstFolder nested)
                yield return nested;
        }
    }

    private static void TraverseFolder(
        XstFolder folder,
        string parentPath,
        int depth,
        ParseContext ctx)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();

        var folderName =
            CleanNullable(GetString(folder, "Name")) ??
            CleanNullable(GetString(folder, "DisplayName")) ??
            "Folder";

        // Το "/" μέσα σε όνομα φακέλου θα έκανε τη διαδρομή διφορούμενη.
        folderName = folderName.Replace('/', '_');

        var folderPath = string.IsNullOrWhiteSpace(parentPath)
            ? folderName
            : $"{parentPath}/{folderName}";

        if (depth > MaxFolderDepth)
        {
            ctx.Report.Warn($"Folder '{folderPath}' skipped: maximum depth ({MaxFolderDepth}) exceeded.");
            return;
        }

        if (!ctx.VisitedFolders.Add(folder))
        {
            ctx.Report.Warn($"Folder '{folderPath}' skipped: already visited (cycle).");
            return;
        }

        ctx.Report.FoldersVisited++;

        try
        {
            foreach (var messageObject in GetEnumerable(folder, "Messages"))
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();

                ParsedPstEmail email;

                try
                {
                    if (messageObject is not XstMessage message)
                    {
                        ctx.Report.MessagesSkipped++;
                        ctx.Report.Warn(
                            $"Unexpected message type in '{folderPath}': {messageObject.GetType().FullName}");
                        continue;
                    }

                    email = MapMessage(message, folderPath, ctx);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Ένα κατεστραμμένο μήνυμα δεν πρέπει να ρίχνει όλο το PST.
                    ctx.Report.MessagesSkipped++;
                    ctx.Report.Warn($"Message in '{folderPath}' skipped: {ex.Message}");
                    continue;
                }

                // Έξω από το try: σφάλμα αποθήκευσης δεν είναι "κατεστραμμένο μήνυμα".
                ctx.OnEmail(email);
                ctx.Report.MessagesParsed++;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ctx.Report.Warn($"Reading messages of folder '{folderPath}' failed: {ex.Message}");
        }

        foreach (var childObject in GetEnumerable(folder, "Folders"))
        {
            if (childObject is XstFolder child)
                TraverseFolder(child, folderPath, depth + 1, ctx);
        }
    }

    // ------------------------------------------------------------------
    // Χαρτογράφηση μηνύματος
    // ------------------------------------------------------------------

    private static ParsedPstEmail MapMessage(
        XstMessage message,
        string folderPath,
        ParseContext ctx)
    {
        var recipientsObject = GetObject(message, "Recipients");

        var senderObject =
            GetObject(recipientsObject, "Sender") ??
            GetObject(recipientsObject, "Originator") ??
            GetObject(recipientsObject, "SentRepresenting") ??
            GetObject(message, "Sender");

        var senderName =
            CleanNullable(
                GetRecipientName(senderObject) ??
                GetString(message, "SenderName") ??
                GetString(message, "FromName") ??
                GetStringFromProperties(message, "SenderName"));

        var subject =
            CleanNullable(
                GetString(message, "Subject") ??
                GetStringFromProperties(message, "Subject")) ??
            "(No subject)";

        var senderEmail = ResolveSenderEmail(message, senderObject, ctx, folderPath, subject);

        // Στάλθηκε: ώρα υποβολής. Η "Date" είναι τελευταία επιλογή γιατί μπορεί να είναι παραλαβή/τροποποίηση.
        var sentAt =
            GetDateTime(message, "SubmittedTime") ??
            GetDateTime(message, "ClientSubmitTime") ??
            GetDateTime(message, "SentAt") ??
            GetDateTime(message, "SubmitTime") ??
            GetDateTimeFromProperties(message, "ClientSubmitTime") ??
            GetDateTime(message, "Date");

        // Παραλήφθηκε: ώρα παράδοσης. Αν λείπει, πέφτουμε στο sentAt.
        var receivedAt =
            GetDateTime(message, "ReceivedTime") ??
            GetDateTime(message, "ReceivedAt") ??
            GetDateTime(message, "DeliveryTime") ??
            GetDateTimeFromProperties(message, "MessageDeliveryTime") ??
            GetDateTimeFromProperties(message, "DeliveryTime") ??
            sentAt;

        var body = ExtractBody(message);

        var internetMessageId =
            CleanNullable(
                GetString(message, "InternetMessageId") ??
                GetString(message, "InternetMessageID") ??
                GetStringFromProperties(message, "InternetMessageId") ??
                GetStringFromProperties(message, "InternetMessageID")) ??
            BuildStableMessageId(senderEmail, subject, sentAt, body.BodyText);

        var recipients = MapRecipients(recipientsObject, ctx.Report);
        var attachments = MapAttachments(message, folderPath, ctx);

        return new ParsedPstEmail(
            InternetMessageId: internetMessageId,
            FolderPath: string.IsNullOrWhiteSpace(folderPath) ? "Root" : folderPath,
            SenderEmail: senderEmail,
            SenderName: senderName,
            Subject: subject,
            BodyText: CleanBody(body.BodyText),
            BodyHtml: string.IsNullOrWhiteSpace(body.BodyHtml) ? null : body.BodyHtml,
            SentAt: sentAt,
            ReceivedAt: receivedAt,
            Recipients: recipients,
            Attachments: attachments);
    }

    private static string ResolveSenderEmail(
        XstMessage message,
        object? senderObject,
        ParseContext ctx,
        string folderPath,
        string subject)
    {
        var email =
            FirstEmail(
                senderObject,
                "SmtpAddress", "EmailAddress", "Email", "Address", "DisplayEmail") ??
            FirstEmail(
                message,
                "SenderSmtpAddress", "SentRepresentingSmtpAddress", "SenderEmail",
                "SenderEmailAddress", "FromEmail");

        if (email != null)
            return email;

        // Σε PST από Exchange ο αποστολέας είναι συχνά διεύθυνση X.500 (/O=...), όχι SMTP.
        ctx.Report.SendersUnresolved++;

        var raw =
            GetString(senderObject, "EmailAddress") ??
            GetString(message, "SenderEmailAddress");

        ctx.Report.Warn(
            $"Sender SMTP address not resolved in '{folderPath}' (subject '{Truncate(subject, 60)}'); " +
            $"raw value: '{Truncate(raw ?? "<none>", 80)}'.");

        return "unknown@unknown.local";
    }

    /// <summary>
    /// Σταθερό ID όταν λείπει το Message-ID: ίδιο μήνυμα => ίδιο ID σε κάθε import (ώστε να δουλεύει το dedup).
    /// </summary>
    private static string BuildStableMessageId(
        string senderEmail,
        string subject,
        DateTime? sentAt,
        string? bodyText)
    {
        var bodyPrefix = string.IsNullOrEmpty(bodyText)
            ? string.Empty
            : bodyText[..Math.Min(bodyText.Length, 500)];

        var raw = string.Join(
            "|",
            senderEmail,
            subject,
            sentAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            bodyPrefix);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        return $"<{Convert.ToHexString(hash)[..32].ToLowerInvariant()}@pst-hash.local>";
    }

    // ------------------------------------------------------------------
    // Σώμα μηνύματος
    // ------------------------------------------------------------------

    private static BodyExtractionResult ExtractBody(XstMessage message)
    {
        string? bodyText = null;
        string? bodyHtml = null;

        // 1. Ρητό BodyHtml.
        bodyHtml = ExtractStringValue(GetPropertyValue(message, "BodyHtml"));

        // 2. Body (string ή αντικείμενο με Text/Bytes/NativeBody).
        var bodyObject = GetPropertyValue(message, "Body");

        if (bodyObject != null)
        {
            var format =
                GetString(bodyObject, "Format") ??
                (GetBool(message, "IsBodyRtf") ? "Rtf" :
                 GetBool(message, "IsBodyHtml") ? "Html" : null);

            var candidates = new[]
            {
                ExtractStringValue(bodyObject),
                ExtractStringValue(GetPropertyValue(bodyObject, "Text")),
                ExtractStringValue(GetPropertyValue(bodyObject, "Bytes")),
                ExtractStringValue(GetPropertyValue(bodyObject, "NativeBody"))
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                AssignBody(candidate, format, ref bodyText, ref bodyHtml);
                break;
            }
        }

        // 3. Άμεσο BodyText.
        if (string.IsNullOrWhiteSpace(bodyText) && string.IsNullOrWhiteSpace(bodyHtml))
        {
            var directText = GetString(message, "BodyText");

            if (!string.IsNullOrWhiteSpace(directText))
                bodyText = NormalizeBodyText(directText);
        }

        // 4. Fallback από τη λίστα ιδιοτήτων.
        if (string.IsNullOrWhiteSpace(bodyText) && string.IsNullOrWhiteSpace(bodyHtml))
        {
            var propertyBody = ExtractStringValue(GetPropertyFromBag(message, "Body"));

            if (!string.IsNullOrWhiteSpace(propertyBody))
                AssignBody(propertyBody, null, ref bodyText, ref bodyHtml);
        }

        // Αν υπάρχει μόνο HTML, παράγουμε το plain text από αυτό.
        if (!string.IsNullOrWhiteSpace(bodyHtml) && string.IsNullOrWhiteSpace(bodyText))
            bodyText = NormalizeBodyText(StripHtml(bodyHtml));

        return new BodyExtractionResult(bodyText, bodyHtml);
    }

    private static void AssignBody(
        string content,
        string? format,
        ref string? bodyText,
        ref string? bodyHtml)
    {
        if (IsRtfBody(content) || StringEqualsLoose(format, "Rtf"))
        {
            var rtf = ConvertRtf(content);

            // RTF με ενσωματωμένο HTML (\fromhtml): κρατάμε και το HTML.
            if (!string.IsNullOrWhiteSpace(rtf.Html))
                bodyHtml = rtf.Html;

            bodyText = rtf.Text;
        }
        else if (StringEqualsLoose(format, "Html") || IsHtmlBody(format, content))
        {
            bodyHtml = content;
            bodyText = NormalizeBodyText(StripHtml(content));
        }
        else
        {
            bodyText = NormalizeBodyText(content);
        }
    }

    private static string? ExtractStringValue(object? value)
    {
        switch (value)
        {
            case null:
                return null;

            case string text:
                return string.IsNullOrWhiteSpace(text) ? null : text;

            case byte[] bytes:
                return DecodeBodyBytes(bytes);

            default:
                return SafeToString(value);
        }
    }

    private static string? DecodeBodyBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        // UTF-16LE χωρίς BOM: πολλά μηδενικά σε περιττές θέσεις.
        if (LooksLikeUtf16Le(bytes))
            return Encoding.Unicode.GetString(bytes);

        try
        {
            // Αυστηρό UTF-8: αν είναι άκυρο, ΔΕΝ γεμίζουμε με '�', πέφτουμε σε κωδικοσελίδα.
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DetectLegacyEncoding(bytes).GetString(bytes);
        }
    }

    private static bool LooksLikeUtf16Le(byte[] bytes)
    {
        var sample = Math.Min(bytes.Length, 128);

        if (sample < 4)
            return false;

        var zeros = 0;
        var pairs = 0;

        for (var i = 1; i < sample; i += 2)
        {
            pairs++;

            if (bytes[i] == 0)
                zeros++;
        }

        return pairs > 0 && zeros * 2 > pairs;
    }

    private static Encoding DetectLegacyEncoding(byte[] bytes)
    {
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 4096));

        var charset = CharsetRegex.Match(head);

        if (charset.Success)
        {
            try
            {
                return Encoding.GetEncoding(charset.Groups[1].Value);
            }
            catch (ArgumentException)
            {
            }
        }

        var ansi = AnsiCodePageRegex.Match(head);

        if (ansi.Success && int.TryParse(ansi.Groups[1].Value, out var codePage))
            return GetEncodingOrLatin1(codePage);

        return GetEncodingOrLatin1(FallbackCodePage);
    }

    private static Encoding GetEncodingOrLatin1(int codePage)
    {
        try
        {
            return Encoding.GetEncoding(codePage);
        }
        catch (Exception)
        {
            return Encoding.Latin1;
        }
    }

    // ------------------------------------------------------------------
    // Παραλήπτες
    // ------------------------------------------------------------------

    private static IReadOnlyCollection<ParsedPstRecipient> MapRecipients(
        object? recipientsObject,
        PstParseReport report)
    {
        var recipients = new List<ParsedPstRecipient>();

        AddRecipients(recipients, recipientsObject, "To", RecipientType.To, report);
        AddRecipients(recipients, recipientsObject, "Cc", RecipientType.Cc, report);
        AddRecipients(recipients, recipientsObject, "Bcc", RecipientType.Bcc, report);

        return recipients
            .GroupBy(x => (x.RecipientType, Email: x.RecipientEmail.ToLowerInvariant()))
            .Select(x => x.First())
            .ToList();
    }

    private static void AddRecipients(
        List<ParsedPstRecipient> recipients,
        object? recipientsObject,
        string propertyName,
        RecipientType recipientType,
        PstParseReport report)
    {
        foreach (var recipientObject in GetEnumerable(recipientsObject, propertyName))
        {
            var email = GetRecipientEmail(recipientObject);

            if (string.IsNullOrWhiteSpace(email))
            {
                // Distribution list ή εσωτερική διεύθυνση Exchange χωρίς SMTP.
                report.RecipientsWithoutEmail++;
                continue;
            }

            recipients.Add(
                new ParsedPstRecipient(
                    recipientType,
                    email,
                    CleanNullable(GetRecipientName(recipientObject))));
        }
    }

    private static string? GetRecipientEmail(object? recipientObject) =>
        FirstEmail(
            recipientObject,
            "SmtpAddress", "EmailAddress", "Email", "Address", "DisplayEmail");

    private static string? GetRecipientName(object? recipientObject)
    {
        if (recipientObject == null)
            return null;

        return GetString(recipientObject, "DisplayName") ??
               GetString(recipientObject, "Name") ??
               GetString(recipientObject, "RecipientName") ??
               GetStringFromProperties(recipientObject, "DisplayName");
    }

    /// <summary>
    /// Επιστρέφει την πρώτη τιμή που κανονικοποιείται σε πραγματικό email
    /// (μια διεύθυνση X.500 δεν «κλειδώνει» την αλυσίδα, δοκιμάζεται η επόμενη).
    /// </summary>
    private static string? FirstEmail(object? source, params string[] propertyNames)
    {
        if (source == null)
            return null;

        foreach (var name in propertyNames)
        {
            var email = NormalizeEmail(GetString(source, name));

            if (email != null)
                return email;
        }

        foreach (var name in propertyNames)
        {
            var email = NormalizeEmail(ValueToString(GetPropertyFromBag(source, name)));

            if (email != null)
                return email;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Συνημμένα
    // ------------------------------------------------------------------

    private static IReadOnlyCollection<ParsedPstAttachment> MapAttachments(
        object message,
        string folderPath,
        ParseContext ctx)
    {
        var attachments = new List<ParsedPstAttachment>();

        foreach (var attachmentObject in GetEnumerable(message, "Attachments"))
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            string? tempFilePath = null;

            try
            {
                if (!GetBool(attachmentObject, "IsFile"))
                {
                    // Ενσωματωμένα μηνύματα / OLE: δεν υποστηρίζονται ακόμα, αλλά καταγράφονται.
                    ctx.Report.AttachmentsSkipped++;
                    ctx.Report.Warn($"Non-file attachment skipped in '{folderPath}'.");
                    continue;
                }

                var fileName =
                    GetString(attachmentObject, "FileName") ??
                    GetString(attachmentObject, "LongFileName") ??
                    GetString(attachmentObject, "DisplayName") ??
                    $"attachment-{Guid.NewGuid():N}.bin";

                var safeFileName = SanitizeFileName(fileName);

                Directory.CreateDirectory(ctx.TempDirectory);

                // Το temp όνομα είναι μοναδικό και κοντό, ανεξάρτητα από το πραγματικό όνομα.
                tempFilePath = Path.Combine(
                    ctx.TempDirectory,
                    $"{++ctx.AttachmentCounter}{Path.GetExtension(safeFileName)}");

                SaveAttachmentToFile(ctx.Xst, attachmentObject, tempFilePath);

                var info = new FileInfo(tempFilePath);

                if (!info.Exists)
                {
                    ctx.Report.AttachmentsSkipped++;
                    ctx.Report.Warn($"Attachment '{safeFileName}' in '{folderPath}' was not written.");
                    continue;
                }

                if (info.Length == 0)
                {
                    ctx.Report.AttachmentsSkipped++;
                    ctx.Report.Warn($"Attachment '{safeFileName}' in '{folderPath}' is empty.");
                    continue;
                }

                if (info.Length > MaxAttachmentBytes)
                {
                    ctx.Report.AttachmentsSkipped++;
                    ctx.Report.Warn(
                        $"Attachment '{safeFileName}' in '{folderPath}' skipped: " +
                        $"{info.Length / (1024 * 1024)} MB exceeds the {MaxAttachmentBytes / (1024 * 1024)} MB limit.");
                    continue;
                }

                attachments.Add(
                    new ParsedPstAttachment(
                        safeFileName,
                        GuessContentType(safeFileName),
                        File.ReadAllBytes(tempFilePath)));

                ctx.Report.AttachmentsSaved++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ctx.Report.AttachmentsSkipped++;
                ctx.Report.Warn($"Attachment in '{folderPath}' failed: {GetInnermostMessage(ex)}");
            }
            finally
            {
                if (tempFilePath != null)
                    TryDeleteFile(tempFilePath);
            }
        }

        return attachments;
    }

    private static void SaveAttachmentToFile(
        XstFile xstFile,
        object attachmentObject,
        string filePath)
    {
        // 1. Attachment.SaveToFile(...)
        var saveMethods = attachmentObject
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.Name == "SaveToFile")
            .OrderBy(x => x.GetParameters().Length)
            .ToList();

        foreach (var method in saveMethods)
        {
            var parameters = method.GetParameters();

            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                continue;

            var args = new object?[parameters.Length];
            args[0] = filePath;

            for (var i = 1; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue
                    ? parameters[i].DefaultValue
                    : CreateDefaultValue(parameters[i].ParameterType);
            }

            try
            {
                method.Invoke(attachmentObject, args);

                if (File.Exists(filePath))
                    return;
            }
            catch (TargetInvocationException)
            {
                // Δοκιμάζουμε την επόμενη υπερφόρτωση.
            }
        }

        // 2. Νεότερες εκδόσεις: XstFile.SaveAttachment(attachment, path)
        var fileMethod = xstFile
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.Name == "SaveAttachment")
            .FirstOrDefault(x =>
            {
                var p = x.GetParameters();
                return p.Length >= 2 &&
                       p[0].ParameterType.IsInstanceOfType(attachmentObject) &&
                       p[1].ParameterType == typeof(string);
            });

        if (fileMethod != null)
        {
            var p = fileMethod.GetParameters();
            var args = new object?[p.Length];
            args[0] = attachmentObject;
            args[1] = filePath;

            for (var i = 2; i < p.Length; i++)
            {
                args[i] = p[i].HasDefaultValue
                    ? p[i].DefaultValue
                    : CreateDefaultValue(p[i].ParameterType);
            }

            fileMethod.Invoke(xstFile, args);

            if (File.Exists(filePath))
                return;
        }

        throw new InvalidOperationException(
            "This XstReader version exposes neither Attachment.SaveToFile nor XstFile.SaveAttachment; " +
            "attachment could not be saved.");
    }

    private static object? CreateDefaultValue(Type type)
    {
        if (Nullable.GetUnderlyingType(type) != null)
            return null;

        if (type == typeof(DateTime))
            return DateTime.UtcNow;

        if (type == typeof(string))
            return string.Empty;

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    // ------------------------------------------------------------------
    // Reflection helpers (με cache)
    // ------------------------------------------------------------------

    private static IEnumerable<object> GetEnumerable(object? source, string propertyName)
    {
        if (source == null)
            yield break;

        var value = GetPropertyValue(source, propertyName);

        if (value is null or string)
            yield break;

        if (value is not IEnumerable enumerable)
            yield break;

        foreach (var item in enumerable)
        {
            if (item != null)
                yield return item;
        }
    }

    private static object? GetObject(object? source, string propertyName) =>
        source == null ? null : GetPropertyValue(source, propertyName);

    private static string? GetString(object? source, string propertyName) =>
        ValueToString(GetPropertyValue(source, propertyName));

    private static DateTime? GetDateTime(object? source, string propertyName) =>
        ValueToDateTime(GetPropertyValue(source, propertyName));

    private static bool GetBool(object? source, string propertyName)
    {
        var value = GetPropertyValue(source, propertyName);

        return value switch
        {
            bool boolean => boolean,
            int number => number != 0,
            long number => number != 0,
            string text when bool.TryParse(text, out var parsed) => parsed,
            string text when int.TryParse(text, out var parsed) => parsed != 0,
            _ => false
        };
    }

    private static object? GetPropertyValue(object? source, string propertyName)
    {
        if (source == null)
            return null;

        var getter = GetterCache.GetOrAdd(
            (source.GetType(), propertyName),
            static key => BuildGetter(key.Item1, key.Item2));

        if (getter == null)
            return null;

        try
        {
            return getter(source);
        }
        catch
        {
            return null;
        }
    }

    private static Func<object, object?>? BuildGetter(Type type, string propertyName)
    {
        try
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

            var property = type.GetProperty(propertyName, flags);

            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                return instance => property.GetValue(instance);

            var field = type.GetField(propertyName, flags);

            if (field != null)
                return instance => field.GetValue(instance);
        }
        catch (AmbiguousMatchException)
        {
        }

        return null;
    }

    private static string? GetStringFromProperties(object? source, string propertyName) =>
        ValueToString(GetPropertyFromBag(source, propertyName));

    private static DateTime? GetDateTimeFromProperties(object? source, string propertyName) =>
        ValueToDateTime(GetPropertyFromBag(source, propertyName));

    /// <summary>Ψάχνει μια ιδιότητα στη γενική λίστα "Properties" του αντικειμένου.</summary>
    private static object? GetPropertyFromBag(object? source, string propertyName)
    {
        var bag = GetPropertyValue(source, "Properties");

        if (bag == null)
            return null;

        foreach (var propertyObject in EnumerateObject(bag))
        {
            var name =
                GetString(propertyObject, "Name") ??
                GetString(propertyObject, "PropertyName") ??
                GetString(propertyObject, "Key") ??
                GetString(propertyObject, "TagName") ??
                GetString(propertyObject, "CanonicalName");

            if (!StringEqualsLoose(name, propertyName))
                continue;

            var value =
                GetPropertyValue(propertyObject, "Value") ??
                GetPropertyValue(propertyObject, "Data") ??
                GetPropertyValue(propertyObject, "PropertyValue");

            if (value != null)
                return value;
        }

        return null;
    }

    private static IEnumerable<object> EnumerateObject(object? value)
    {
        if (value == null || value is string)
            yield break;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Value != null)
                    yield return entry.Value;
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    yield return item;
            }
        }
    }

    // ------------------------------------------------------------------
    // Μετατροπές τιμών
    // ------------------------------------------------------------------

    private static string? ValueToString(object? value)
    {
        return value switch
        {
            null => null,
            string text when string.IsNullOrWhiteSpace(text) => null,
            string text => text.Trim(),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToString("O", CultureInfo.InvariantCulture),
            // Ένα byte[] ΔΕΝ είναι κείμενο: δεν το μετατρέπουμε σε Base64 "σώμα".
            byte[] => null,
            _ => SafeToString(value)
        };
    }

    private static string? SafeToString(object value)
    {
        var text = value.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var type = value.GetType();
        var typeName = type.FullName ?? type.Name;

        // Το προεπιλεγμένο ToString() επιστρέφει όνομα τύπου (και για generics).
        if (text == type.Name ||
            text.StartsWith(typeName, StringComparison.Ordinal) ||
            text.StartsWith(type.Name + "`", StringComparison.Ordinal))
        {
            return null;
        }

        return text;
    }

    /// <summary>Οι ώρες των PST αποθηκεύονται σε UTC (FILETIME), άρα το Unspecified θεωρείται UTC.</summary>
    private static DateTime? ValueToDateTime(object? value)
    {
        return value switch
        {
            null => null,

            DateTime dateTime =>
                dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    : dateTime.ToUniversalTime(),

            DateTimeOffset offset => offset.UtcDateTime,

            string text when DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) => parsed,

            _ => null
        };
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var match = EmailRegex.Match(email.Trim());

        // Διευθύνσεις X.500 (/O=...) δεν περιέχουν έγκυρο email => null.
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    // ------------------------------------------------------------------
    // Κείμενο / HTML
    // ------------------------------------------------------------------

    /// <summary>Για μονόγραμμα πεδία (θέμα, ονόματα): όλα τα κενά γίνονται ένα κενό.</summary>
    private static string? CleanNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return InlineWhitespaceRegex.Replace(value, " ").Trim();
    }

    /// <summary>Για σώμα: κρατά τις αλλαγές γραμμής και τις παραγράφους.</summary>
    private static string? CleanBody(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = NormalizeBodyText(value);

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeBodyText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace('\u00A0', ' ');

        text = HorizontalWhitespaceRegex.Replace(text, " ");
        text = SpacesAroundNewlineRegex.Replace(text, "\n");
        text = ManyNewlinesRegex.Replace(text, "\n\n");

        return text.Trim();
    }

    private static bool IsHtmlBody(string? bodyFormat, string body)
    {
        if (!string.IsNullOrWhiteSpace(bodyFormat) &&
            bodyFormat.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Αναζήτηση γνωστών HTML tags (όχι απλά "</"), ώστε κώδικας/XML σε plain text να μη θεωρείται HTML.
        return HtmlMarkerRegex.IsMatch(body);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = ScriptRegex.Replace(html, " ");
        text = StyleRegex.Replace(text, " ");
        text = HtmlCommentRegex.Replace(text, " ");
        text = HtmlBreakRegex.Replace(text, "\n");
        text = HtmlCellRegex.Replace(text, " ");
        text = HtmlTagRegex.Replace(text, string.Empty);

        return WebUtility.HtmlDecode(text);
    }

    private static bool StringEqualsLoose(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return false;

        return string.Equals(
            NonAlphanumericRegex.Replace(left, string.Empty),
            NonAlphanumericRegex.Replace(right, string.Empty),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    // ------------------------------------------------------------------
    // Ονόματα αρχείων / content types
    // ------------------------------------------------------------------

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();

        var sanitized = new string(
            fileName.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            .Trim()
            .TrimEnd('.');

        if (string.IsNullOrWhiteSpace(sanitized) || sanitized.All(c => c == '.' || c == '_'))
            return $"attachment-{Guid.NewGuid():N}.bin";

        var extension = Path.GetExtension(sanitized);
        var baseName = Path.GetFileNameWithoutExtension(sanitized);

        if (ReservedWindowsNames.Contains(baseName))
            baseName = "_" + baseName;

        if (extension.Length > 16)
            extension = extension[..16];

        var maxBase = Math.Max(1, MaxFileNameLength - extension.Length);

        if (baseName.Length > maxBase)
            baseName = baseName[..maxBase];

        return baseName + extension;
    }

    private static string GuessContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    // ------------------------------------------------------------------
    // Βοηθητικά αρχείων / σφαλμάτων
    // ------------------------------------------------------------------

    private static string GetInnermostMessage(Exception exception)
    {
        var current = exception;

        while (current.InnerException != null)
            current = current.InnerException;

        return current.Message;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Σφάλματα καθαρισμού temp αγνοούνται (ο φάκελος σβήνεται στο τέλος).
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, recursive: true);
        }
        catch
        {
        }
    }

    private static void ValidatePstHeader(string pstFilePath)
    {
        const int headerLength = 12;

        var fileInfo = new FileInfo(pstFilePath);

        if (fileInfo.Length < headerLength)
            throw new InvalidDataException("InvalidPstFile: file is too small to be a valid PST/OST file.");

        var header = new byte[headerLength];

        using (var stream = File.OpenRead(pstFilePath))
        {
            var read = 0;

            while (read < headerLength)
            {
                var n = stream.Read(header, read, headerLength - read);

                if (n == 0)
                    break;

                read += n;
            }

            if (read < headerLength)
                throw new InvalidDataException("InvalidPstFile: could not read PST/OST file header.");
        }

        // dwMagic = "!BDN"
        if (header[0] != 0x21 || header[1] != 0x42 || header[2] != 0x44 || header[3] != 0x4E)
            throw new InvalidDataException("InvalidPstFile: missing PST/OST magic header.");

        // wVer (offset 10): 14/15 = ANSI, 21/23 = Unicode, 36/37 = Unicode 4K.
        var version = BitConverter.ToUInt16(header, 10);

        if (version is not (14 or 15 or 17 or 21 or 23 or 36 or 37))
            throw new InvalidDataException($"InvalidPstFile: unsupported PST/OST version ({version}).");
    }

    // ------------------------------------------------------------------
    // RTF → κείμενο (με κωδικοσελίδες, \ucN και αποκωδικοποίηση \fromhtml)
    // ------------------------------------------------------------------

    private static bool IsRtfBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        return body.TrimStart().StartsWith(@"{\rtf", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct RtfState(bool Ignore, bool HtmlRtf, bool HtmlTag, int Uc);

    private sealed record RtfResult(string Text, string? Html);

    private static RtfResult ConvertRtf(string rtf)
    {
        var fromHtml = rtf.Contains(@"\fromhtml", StringComparison.OrdinalIgnoreCase);

        var encoding = Encoding.Latin1;
        var ansi = AnsiCodePageRegex.Match(rtf);

        encoding = ansi.Success && int.TryParse(ansi.Groups[1].Value, out var codePage)
            ? GetEncodingOrLatin1(codePage)
            : GetEncodingOrLatin1(1252);

        var text = new StringBuilder();
        var html = new StringBuilder();
        var pendingBytes = new List<byte>();
        var stack = new Stack<RtfState>();
        var state = new RtfState(Ignore: false, HtmlRtf: false, HtmlTag: false, Uc: 1);
        var skip = 0;
        var pendingStar = false;

        void EmitText(string value)
        {
            if (state.Ignore)
                return;

            if (fromHtml)
            {
                if (!state.HtmlRtf)
                    html.Append(value);
            }
            else
            {
                text.Append(value);
            }
        }

        void FlushBytes()
        {
            if (pendingBytes.Count == 0)
                return;

            var decoded = encoding.GetString(pendingBytes.ToArray());
            pendingBytes.Clear();
            EmitText(decoded);
        }

        for (var i = 0; i < rtf.Length; i++)
        {
            var c = rtf[i];

            if (c == '{')
            {
                FlushBytes();
                stack.Push(state);
                pendingStar = false;
                continue;
            }

            if (c == '}')
            {
                FlushBytes();

                if (stack.Count > 0)
                    state = stack.Pop();

                pendingStar = false;
                continue;
            }

            // Οι φυσικές αλλαγές γραμμής μέσα στο RTF δεν είναι περιεχόμενο.
            if (c == '\r' || c == '\n')
                continue;

            if (c != '\\')
            {
                if (skip > 0)
                {
                    skip--;
                    continue;
                }

                FlushBytes();
                EmitText(c.ToString());
                continue;
            }

            if (i + 1 >= rtf.Length)
                break;

            var next = rtf[i + 1];

            if (next is '\\' or '{' or '}')
            {
                i++;

                if (skip > 0)
                {
                    skip--;
                    continue;
                }

                FlushBytes();
                EmitText(next.ToString());
                continue;
            }

            if (next == '*')
            {
                pendingStar = true;
                i++;
                continue;
            }

            if (next == '\'')
            {
                if (i + 3 < rtf.Length &&
                    byte.TryParse(
                        rtf.AsSpan(i + 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var byteValue))
                {
                    if (skip > 0)
                        skip--;
                    else if (!state.Ignore)
                        pendingBytes.Add(byteValue);

                    i += 3;
                }
                else
                {
                    i++;
                }

                continue;
            }

            if (next == '~')
            {
                FlushBytes();
                EmitText(" ");
                i++;
                continue;
            }

            if (next == '_')
            {
                FlushBytes();
                EmitText("-");
                i++;
                continue;
            }

            if (next == '-')
            {
                i++;
                continue;
            }

            if (next is '\r' or '\n')
            {
                FlushBytes();

                if (!fromHtml)
                    EmitText("\n");

                i++;
                continue;
            }

            if (!char.IsLetter(next))
            {
                i++;
                continue;
            }

            // Control word.
            FlushBytes();

            var wordStart = i + 1;
            var cursor = wordStart;

            while (cursor < rtf.Length && char.IsAsciiLetter(rtf[cursor]))
                cursor++;

            var word = rtf[wordStart..cursor].ToLowerInvariant();

            var parameterStart = cursor;

            if (cursor < rtf.Length && (rtf[cursor] == '-' || rtf[cursor] == '+'))
                cursor++;

            while (cursor < rtf.Length && char.IsAsciiDigit(rtf[cursor]))
                cursor++;

            var parameter = rtf[parameterStart..cursor];

            // Ένα κενό μετά το control word είναι διαχωριστικό και καταναλώνεται.
            i = cursor < rtf.Length && rtf[cursor] == ' ' ? cursor : cursor - 1;

            if (pendingStar)
            {
                pendingStar = false;

                state = fromHtml && word == "htmltag"
                    ? state with { HtmlTag = true }
                    : state with { Ignore = true };

                continue;
            }

            if (IsIgnoredRtfDestination(word))
            {
                state = state with { Ignore = true };
                continue;
            }

            switch (word)
            {
                case "htmlrtf":
                    state = state with { HtmlRtf = parameter != "0" };
                    break;

                case "uc":
                    if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uc))
                        state = state with { Uc = Math.Max(0, uc) };
                    break;

                case "u":
                    if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                    {
                        if (code < 0)
                            code += 65536;

                        EmitText(((char)code).ToString());
                        skip = state.Uc;
                    }
                    break;

                case "par":
                case "line":
                    if (!fromHtml)
                        EmitText("\n");
                    break;

                case "tab":
                    if (!fromHtml)
                        EmitText("\t");
                    break;

                case "emdash": EmitText("—"); break;
                case "endash": EmitText("–"); break;
                case "bullet": EmitText("•"); break;
                case "lquote": EmitText("‘"); break;
                case "rquote": EmitText("’"); break;
                case "ldblquote": EmitText("“"); break;
                case "rdblquote": EmitText("”"); break;
            }
        }

        FlushBytes();

        if (fromHtml)
        {
            var extractedHtml = html.ToString();

            return new RtfResult(
                NormalizeBodyText(StripHtml(extractedHtml)),
                string.IsNullOrWhiteSpace(extractedHtml) ? null : extractedHtml);
        }

        return new RtfResult(NormalizeBodyText(text.ToString()), null);
    }

    private static bool IsIgnoredRtfDestination(string word) =>
        word is
            "fonttbl" or "colortbl" or "stylesheet" or "info" or "pict" or "object" or
            "generator" or "xmlnstbl" or "datastore" or "themedata" or "colorschememapping" or
            "listtable" or "listoverridetable" or "rsidtbl" or "latentstyles" or "header" or "footer";

    private sealed record BodyExtractionResult(string? BodyText, string? BodyHtml);
}