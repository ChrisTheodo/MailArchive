using MailArchive.Domain.Enums;
using System.Collections;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using XstReader;

namespace MailArchive.Application.Imports.Parsing;

public class XstReaderPstParser : IPstParser
{
    public Task<IReadOnlyCollection<ParsedPstEmail>> ParseAsync(
        string pstFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pstFilePath))
            throw new ArgumentException(
                "PST file path is required.",
                nameof(pstFilePath));

        if (!File.Exists(pstFilePath))
            throw new FileNotFoundException(
                "PST file was not found.",
                pstFilePath);

        ValidatePstHeader(pstFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var parsedEmails =
                new List<ParsedPstEmail>();

            var xstFile =
                new XstFile(pstFilePath);

            /*
             * Do not use ReadMessages() or ReadMessageDetails().
             *
             * Those methods exist in some XstReader versions,
             * but are not exposed by the XstReader version used
             * by this project.
             *
             * The current API loads message data through the
             * exposed folder/message properties.
             */
            var rootFolderObject =
                GetObject(xstFile, "RootFolder");

            if (rootFolderObject is XstFolder rootFolder)
            {
                TraverseFolder(
                    folder: rootFolder,
                    parentPath: string.Empty,
                    parsedEmails: parsedEmails,
                    cancellationToken: cancellationToken);
            }
            else
            {
                /*
                 * Some XstReader versions expose the folder tree
                 * through ReadFolderTree() instead of RootFolder.
                 */
                var readFolderTreeMethod =
                    xstFile.GetType().GetMethod(
                        "ReadFolderTree",
                        BindingFlags.Instance |
                        BindingFlags.Public);

                if (readFolderTreeMethod == null)
                {
                    throw new InvalidOperationException(
                        "XstReader does not expose a readable folder tree.");
                }

                var tree =
                    readFolderTreeMethod.Invoke(
                        xstFile,
                        null);

                if (tree is XstFolder rootTreeFolder)
                {
                    TraverseFolder(
                        folder: rootTreeFolder,
                        parentPath: string.Empty,
                        parsedEmails: parsedEmails,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    /*
                     * Handle the API where the returned root object
                     * is not directly XstFolder.
                     */
                    foreach (var folderObject in GetEnumerable(
                                 tree,
                                 "Folders"))
                    {
                        if (folderObject is not XstFolder nestedFolder)
                            continue;

                        TraverseFolder(
                            folder: nestedFolder,
                            parentPath: string.Empty,
                            parsedEmails: parsedEmails,
                            cancellationToken: cancellationToken);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyCollection<ParsedPstEmail>>(
                parsedEmails);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"XstReaderParsingFailed: {GetInnermostMessage(ex)}",
                ex);
        }
    }

    private static void TraverseFolder(
        XstFolder folder,
        string parentPath,
        List<ParsedPstEmail> parsedEmails,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var folderName =
            GetString(folder, "Name") ??
            GetString(folder, "DisplayName") ??
            "Folder";

        var folderPath =
            string.IsNullOrWhiteSpace(parentPath)
                ? folderName
                : $"{parentPath}/{folderName}";

        /*
         * IMPORTANT:
         *
         * Do NOT call XstFile.ReadMessages(folder) here.
         *
         * The installed XstReader version does not expose that method.
         *
         * Accessing folder.Messages causes the library to obtain the
         * message collection through its normal API.
         */
        foreach (var messageObject in GetEnumerable(
                     folder,
                     "Messages"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (messageObject is not XstMessage message)
                {
                    Console.WriteLine(
                        $"[XstReaderPstParser] Unexpected message type in '{folderPath}': " +
                        $"{messageObject.GetType().FullName}");

                    continue;
                }

                parsedEmails.Add(
                    MapMessage(
                        message,
                        folderPath));
            }
            catch (Exception ex)
            {
                /*
                 * One malformed message must not fail the whole PST.
                 */
                Console.WriteLine(
                    $"[XstReaderPstParser] Message in '{folderPath}' skipped: " +
                    $"{ex.Message}");
            }
        }

        foreach (var childFolderObject in GetEnumerable(
                     folder,
                     "Folders"))
        {
            if (childFolderObject is not XstFolder childFolder)
                continue;

            TraverseFolder(
                folder: childFolder,
                parentPath: folderPath,
                parsedEmails: parsedEmails,
                cancellationToken: cancellationToken);
        }
    }

    private static ParsedPstEmail MapMessage(
        XstMessage message,
        string folderPath)
    {
        /*
         * Do NOT call:
         *
         * xstFile.ReadMessageDetails(message)
         *
         * because that method is not available in the installed
         * XstReader version.
         *
         * The message properties are accessed directly below.
         */

        var recipientsObject =
            GetObject(message, "Recipients");

        var senderObject =
            GetObject(recipientsObject, "Sender") ??
            GetObject(recipientsObject, "Originator") ??
            GetObject(recipientsObject, "SentRepresenting") ??
            GetObject(message, "Sender");

        var senderEmail =
            NormalizeEmail(
                GetRecipientEmail(senderObject)) ??
            NormalizeEmail(
                GetString(message, "SenderEmail")) ??
            NormalizeEmail(
                GetString(message, "SenderEmailAddress")) ??
            NormalizeEmail(
                GetString(message, "FromEmail")) ??
            NormalizeEmail(
                GetStringFromProperties(
                    message,
                    "SenderEmailAddress")) ??
            NormalizeEmail(
                GetStringFromProperties(
                    message,
                    "SenderEmail")) ??
            "unknown@unknown.local";

        var senderName =
            GetRecipientName(senderObject) ??
            GetString(message, "SenderName") ??
            GetString(message, "FromName") ??
            GetStringFromProperties(
                message,
                "SenderName");

        var subject =
            GetString(message, "Subject") ??
            GetStringFromProperties(
                message,
                "Subject") ??
            "(No subject)";

        var internetMessageId =
            GetString(message, "InternetMessageId") ??
            GetString(message, "InternetMessageID") ??
            GetStringFromProperties(
                message,
                "InternetMessageId") ??
            GetStringFromProperties(
                message,
                "InternetMessageID") ??
            $"<{Guid.NewGuid():N}@xstreader.local>";

        var sentAt =
            GetDateTime(message, "Date") ??
            GetDateTime(message, "SubmittedTime") ??
            GetDateTime(message, "SentAt") ??
            GetDateTime(message, "SubmitTime") ??
            GetDateTime(message, "ClientSubmitTime") ??
            GetDateTime(message, "DeliveryTime") ??
            GetDateTimeFromProperties(
                message,
                "ClientSubmitTime") ??
            GetDateTimeFromProperties(
                message,
                "DeliveryTime");

        var receivedAt =
            GetDateTime(message, "ReceivedTime") ??
            GetDateTime(message, "ReceivedAt") ??
            GetDateTime(message, "DeliveryTime") ??
            GetDateTimeFromProperties(
                message,
                "DeliveryTime") ??
            sentAt;

        /*
         * BODY
         *
         * Try the different body representations exposed by
         * different XstReader versions.
         */
        var bodyResult =
            ExtractBody(message);

        var recipients =
            MapRecipients(recipientsObject);

        var attachments =
            MapAttachments(message);

        return new ParsedPstEmail(
            InternetMessageId: internetMessageId,
            FolderPath: string.IsNullOrWhiteSpace(folderPath)
                ? "Root"
                : folderPath,
            SenderEmail: senderEmail,
            SenderName: CleanNullable(senderName),
            Subject: CleanNullable(subject) ?? "(No subject)",
            BodyText: CleanNullable(bodyResult.BodyText),
            BodyHtml: CleanNullable(bodyResult.BodyHtml),
            SentAt: sentAt,
            ReceivedAt: receivedAt,
            Recipients: recipients,
            Attachments: attachments);
    }

    private static BodyExtractionResult ExtractBody(
        XstMessage message)
    {
        string? bodyHtml = null;
        string? bodyText = null;

        /*
         * 1. Explicit BodyHtml property.
         */
        var bodyHtmlObject =
            GetPropertyValue(
                message,
                "BodyHtml");

        bodyHtml =
            ExtractStringValue(
                bodyHtmlObject);

        /*
         * 2. Body property.
         */
        var bodyObject =
            GetPropertyValue(
                message,
                "Body");

        if (bodyObject != null)
        {
            /*
             * Body may directly be a string.
             */
            var directBody =
                ExtractStringValue(
                    bodyObject);

            if (!string.IsNullOrWhiteSpace(directBody))
            {
                var bodyFormat =
                    GetString(
                        bodyObject,
                        "Format");

                var isRtf =
                    IsRtfBody(directBody) ||
                    GetBool(
                        message,
                        "IsBodyRtf");

                var isHtml =
                    GetBool(
                        message,
                        "IsBodyHtml") ||
                    IsHtmlBody(
                        bodyFormat,
                        directBody);

                if (isRtf)
                {
                    bodyText =
                        ConvertRtfToPlainText(
                            directBody);
                }
                else if (isHtml)
                {
                    bodyHtml =
                        directBody;

                    bodyText =
                        NormalizeWhitespace(
                            StripHtml(
                                directBody));
                }
                else
                {
                    bodyText =
                        NormalizeWhitespace(
                            directBody);
                }
            }

            /*
             * Body.Text.
             */
            if (string.IsNullOrWhiteSpace(bodyText) &&
                string.IsNullOrWhiteSpace(bodyHtml))
            {
                var bodyTextObject =
                    GetPropertyValue(
                        bodyObject,
                        "Text");

                var nestedText =
                    ExtractStringValue(
                        bodyTextObject);

                if (!string.IsNullOrWhiteSpace(nestedText))
                {
                    var format =
                        GetString(
                            bodyObject,
                            "Format");

                    if (IsRtfBody(nestedText) ||
                        StringEqualsLoose(
                            format,
                            "Rtf"))
                    {
                        bodyText =
                            ConvertRtfToPlainText(
                                nestedText);
                    }
                    else if (
                        StringEqualsLoose(
                            format,
                            "Html") ||
                        IsHtmlBody(
                            format,
                            nestedText))
                    {
                        bodyHtml =
                            nestedText;

                        bodyText =
                            NormalizeWhitespace(
                                StripHtml(
                                    nestedText));
                    }
                    else
                    {
                        bodyText =
                            NormalizeWhitespace(
                                nestedText);
                    }
                }
            }

            /*
             * Body.Bytes.
             */
            if (string.IsNullOrWhiteSpace(bodyText) &&
                string.IsNullOrWhiteSpace(bodyHtml))
            {
                var bodyBytesObject =
                    GetPropertyValue(
                        bodyObject,
                        "Bytes");

                if (bodyBytesObject is byte[] bytes &&
                    bytes.Length > 0)
                {
                    var decoded =
                        DecodeBodyBytes(bytes);

                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        var format =
                            GetString(
                                bodyObject,
                                "Format");

                        if (StringEqualsLoose(
                                format,
                                "Rtf") ||
                            IsRtfBody(decoded))
                        {
                            bodyText =
                                ConvertRtfToPlainText(
                                    decoded);
                        }
                        else if (
                            StringEqualsLoose(
                                format,
                                "Html") ||
                            IsHtmlBody(
                                format,
                                decoded))
                        {
                            bodyHtml =
                                decoded;

                            bodyText =
                                NormalizeWhitespace(
                                    StripHtml(
                                        decoded));
                        }
                        else
                        {
                            bodyText =
                                NormalizeWhitespace(
                                    decoded);
                        }
                    }
                }
            }

            /*
             * NativeBody.
             */
            if (string.IsNullOrWhiteSpace(bodyText) &&
                string.IsNullOrWhiteSpace(bodyHtml))
            {
                var nativeBody =
                    GetPropertyValue(
                        bodyObject,
                        "NativeBody");

                var nativeText =
                    ExtractStringValue(
                        nativeBody);

                if (!string.IsNullOrWhiteSpace(nativeText))
                {
                    if (IsRtfBody(nativeText))
                    {
                        bodyText =
                            ConvertRtfToPlainText(
                                nativeText);
                    }
                    else if (
                        IsHtmlBody(
                            null,
                            nativeText))
                    {
                        bodyHtml =
                            nativeText;

                        bodyText =
                            NormalizeWhitespace(
                                StripHtml(
                                    nativeText));
                    }
                    else
                    {
                        bodyText =
                            NormalizeWhitespace(
                                nativeText);
                    }
                }
            }
        }

        /*
         * 3. Direct BodyText property.
         */
        if (string.IsNullOrWhiteSpace(bodyText) &&
            string.IsNullOrWhiteSpace(bodyHtml))
        {
            var directBodyText =
                GetString(
                    message,
                    "BodyText");

            if (!string.IsNullOrWhiteSpace(directBodyText))
            {
                bodyText =
                    NormalizeWhitespace(
                        directBodyText);
            }
        }

        /*
         * 4. BodyHtml direct fallback.
         */
        if (string.IsNullOrWhiteSpace(bodyHtml))
        {
            var directHtml =
                GetString(
                    message,
                    "BodyHtml");

            if (!string.IsNullOrWhiteSpace(directHtml))
            {
                bodyHtml =
                    directHtml;
            }
        }

        /*
         * 5. Properties fallback.
         */
        if (string.IsNullOrWhiteSpace(bodyText) &&
            string.IsNullOrWhiteSpace(bodyHtml))
        {
            var propertyBody =
                GetStringFromProperties(
                    message,
                    "Body");

            if (!string.IsNullOrWhiteSpace(propertyBody))
            {
                if (IsRtfBody(propertyBody))
                {
                    bodyText =
                        ConvertRtfToPlainText(
                            propertyBody);
                }
                else if (
                    IsHtmlBody(
                        null,
                        propertyBody))
                {
                    bodyHtml =
                        propertyBody;

                    bodyText =
                        NormalizeWhitespace(
                            StripHtml(
                                propertyBody));
                }
                else
                {
                    bodyText =
                        NormalizeWhitespace(
                            propertyBody);
                }
            }
        }

        /*
         * If HTML exists but plain text does not,
         * derive the plain text from HTML.
         */
        if (!string.IsNullOrWhiteSpace(bodyHtml) &&
            string.IsNullOrWhiteSpace(bodyText))
        {
            bodyText =
                NormalizeWhitespace(
                    StripHtml(
                        bodyHtml));
        }

        return new BodyExtractionResult(
            BodyText: bodyText,
            BodyHtml: bodyHtml);
    }

    private static string? ExtractStringValue(
        object? value)
    {
        if (value == null)
            return null;

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? null
                : text;
        }

        return SafeToString(value);
    }

    private static string? DecodeBodyBytes(
        byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;

        /*
         * UTF-16 LE BOM.
         */
        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(
                bytes,
                2,
                bytes.Length - 2);
        }

        /*
         * UTF-8 BOM.
         */
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(
                bytes,
                3,
                bytes.Length - 3);
        }

        /*
         * UTF-8.
         */
        try
        {
            var utf8 =
                Encoding.UTF8.GetString(
                    bytes);

            if (!string.IsNullOrWhiteSpace(utf8))
                return utf8;
        }
        catch
        {
        }

        /*
         * UTF-16 fallback.
         */
        try
        {
            return Encoding.Unicode.GetString(
                bytes);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyCollection<ParsedPstRecipient> MapRecipients(
        object? recipientsObject)
    {
        var recipients =
            new List<ParsedPstRecipient>();

        AddRecipients(
            recipients,
            recipientsObject,
            "To",
            RecipientType.To);

        AddRecipients(
            recipients,
            recipientsObject,
            "Cc",
            RecipientType.Cc);

        AddRecipients(
            recipients,
            recipientsObject,
            "Bcc",
            RecipientType.Bcc);

        return recipients
            .GroupBy(
                x => new
                {
                    x.RecipientType,
                    RecipientEmail =
                        x.RecipientEmail
                            .ToLowerInvariant()
                })
            .Select(
                x => x.First())
            .ToList();
    }

    private static void AddRecipients(
        List<ParsedPstRecipient> recipients,
        object? recipientsObject,
        string propertyName,
        RecipientType recipientType)
    {
        foreach (var recipientObject in GetEnumerable(
                     recipientsObject,
                     propertyName))
        {
            var email =
                NormalizeEmail(
                    GetRecipientEmail(
                        recipientObject));

            if (string.IsNullOrWhiteSpace(email))
                continue;

            var name =
                CleanNullable(
                    GetRecipientName(
                        recipientObject));

            recipients.Add(
                new ParsedPstRecipient(
                    recipientType,
                    email,
                    name));
        }
    }

    private static IReadOnlyCollection<ParsedPstAttachment> MapAttachments(
        object message)
    {
        var attachments =
            new List<ParsedPstAttachment>();

        foreach (var attachmentObject in GetEnumerable(
                     message,
                     "Attachments"))
        {
            try
            {
                var isFile =
                    GetBool(
                        attachmentObject,
                        "IsFile");

                if (!isFile)
                    continue;

                var fileName =
                    GetString(
                        attachmentObject,
                        "FileName") ??
                    GetString(
                        attachmentObject,
                        "LongFileName") ??
                    GetString(
                        attachmentObject,
                        "DisplayName") ??
                    $"attachment-{Guid.NewGuid():N}.bin";

                var safeFileName =
                    SanitizeFileName(
                        fileName);

                var tempFilePath =
                    Path.Combine(
                        Path.GetTempPath(),
                        $"xstreader-{Guid.NewGuid():N}-{safeFileName}");

                try
                {
                    SaveAttachmentToFile(
                        attachmentObject,
                        tempFilePath);

                    if (!File.Exists(tempFilePath))
                        continue;

                    var bytes =
                        File.ReadAllBytes(
                            tempFilePath);

                    if (bytes.Length == 0)
                        continue;

                    attachments.Add(
                        new ParsedPstAttachment(
                            safeFileName,
                            GuessContentType(
                                safeFileName),
                            bytes));
                }
                finally
                {
                    TryDeleteFile(
                        tempFilePath);
                }
            }
            catch
            {
                /*
                 * Broken attachment should not stop PST parsing.
                 */
            }
        }

        return attachments;
    }

    private static void SaveAttachmentToFile(
        object attachmentObject,
        string filePath)
    {
        var attachmentType =
            attachmentObject.GetType();

        var saveMethods =
            attachmentType
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public)
                .Where(
                    x => x.Name == "SaveToFile")
                .OrderBy(
                    x => x.GetParameters().Length)
                .ToList();

        if (saveMethods.Count == 0)
        {
            /*
             * Newer XstReader versions expose saving through
             * XstFile.SaveAttachment rather than Attachment.SaveToFile.
             *
             * If this happens, the parser cannot save the attachment
             * through the current reflection-only implementation.
             */
            throw new InvalidOperationException(
                "XstReader attachment does not expose SaveToFile method.");
        }

        foreach (var method in saveMethods)
        {
            var parameters =
                method.GetParameters();

            try
            {
                if (parameters.Length == 1)
                {
                    method.Invoke(
                        attachmentObject,
                        new object?[]
                        {
                            filePath
                        });

                    return;
                }

                if (parameters.Length == 2)
                {
                    var secondParameterValue =
                        CreateDefaultValue(
                            parameters[1].ParameterType);

                    method.Invoke(
                        attachmentObject,
                        new object?[]
                        {
                            filePath,
                            secondParameterValue
                        });

                    return;
                }
            }
            catch
            {
                /*
                 * Try next overload.
                 */
            }
        }

        throw new InvalidOperationException(
            "Unable to save XstReader attachment.");
    }

    private static object? CreateDefaultValue(
        Type type)
    {
        if (type == typeof(DateTime))
            return DateTime.UtcNow;

        if (type == typeof(DateTime?))
            return DateTime.UtcNow;

        if (type == typeof(bool))
            return false;

        if (type == typeof(string))
            return string.Empty;

        return type.IsValueType
            ? Activator.CreateInstance(type)
            : null;
    }

    private static string? GetRecipientEmail(
        object? recipientObject)
    {
        if (recipientObject == null)
            return null;

        return GetString(
                   recipientObject,
                   "EmailAddress") ??
               GetString(
                   recipientObject,
                   "Email") ??
               GetString(
                   recipientObject,
                   "SmtpAddress") ??
               GetString(
                   recipientObject,
                   "Address") ??
               GetString(
                   recipientObject,
                   "DisplayEmail") ??
               GetStringFromProperties(
                   recipientObject,
                   "EmailAddress") ??
               GetStringFromProperties(
                   recipientObject,
                   "SmtpAddress");
    }

    private static string? GetRecipientName(
        object? recipientObject)
    {
        if (recipientObject == null)
            return null;

        return GetString(
                   recipientObject,
                   "DisplayName") ??
               GetString(
                   recipientObject,
                   "Name") ??
               GetString(
                   recipientObject,
                   "RecipientName") ??
               GetStringFromProperties(
                   recipientObject,
                   "DisplayName");
    }

    private static IEnumerable<object> GetEnumerable(
        object? source,
        string propertyName)
    {
        if (source == null)
            yield break;

        var value =
            GetPropertyValue(
                source,
                propertyName);

        if (value is null)
            yield break;

        if (value is string)
            yield break;

        if (value is not IEnumerable enumerable)
            yield break;

        foreach (var item in enumerable)
        {
            if (item != null)
                yield return item;
        }
    }

    private static object? GetObject(
        object? source,
        string propertyName)
    {
        if (source == null)
            return null;

        return GetPropertyValue(
            source,
            propertyName);
    }

    private static string? GetString(
        object? source,
        string propertyName)
    {
        var value =
            GetPropertyValue(
                source,
                propertyName);

        return ValueToString(value);
    }

    private static DateTime? GetDateTime(
        object? source,
        string propertyName)
    {
        var value =
            GetPropertyValue(
                source,
                propertyName);

        return ValueToDateTime(value);
    }

    private static bool GetBool(
        object? source,
        string propertyName)
    {
        var value =
            GetPropertyValue(
                source,
                propertyName);

        return value switch
        {
            bool boolean =>
                boolean,

            int number =>
                number != 0,

            long number =>
                number != 0,

            string text when bool.TryParse(
                text,
                out var parsed) =>
                parsed,

            string text when int.TryParse(
                text,
                out var parsed) =>
                parsed != 0,

            _ =>
                false
        };
    }

    private static object? GetPropertyValue(
        object? source,
        string propertyName)
    {
        if (source == null)
            return null;

        var type =
            source.GetType();

        try
        {
            var property =
                type.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.IgnoreCase);

            if (property != null &&
                property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(source);
            }

            var field =
                type.GetField(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.IgnoreCase);

            if (field != null)
                return field.GetValue(source);
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? GetStringFromProperties(
        object? source,
        string propertyName)
    {
        if (source == null)
            return null;

        var propertiesObject =
            GetPropertyValue(
                source,
                "Properties");

        if (propertiesObject == null)
            return null;

        foreach (var propertyObject in EnumerateObject(
                     propertiesObject))
        {
            var name =
                GetString(
                    propertyObject,
                    "Name") ??
                GetString(
                    propertyObject,
                    "PropertyName") ??
                GetString(
                    propertyObject,
                    "Key") ??
                GetString(
                    propertyObject,
                    "TagName") ??
                GetString(
                    propertyObject,
                    "CanonicalName");

            if (!StringEqualsLoose(
                    name,
                    propertyName))
            {
                continue;
            }

            var value =
                GetPropertyValue(
                    propertyObject,
                    "Value") ??
                GetPropertyValue(
                    propertyObject,
                    "Data") ??
                GetPropertyValue(
                    propertyObject,
                    "PropertyValue");

            var text =
                ValueToString(value);

            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private static DateTime? GetDateTimeFromProperties(
        object? source,
        string propertyName)
    {
        if (source == null)
            return null;

        var propertiesObject =
            GetPropertyValue(
                source,
                "Properties");

        if (propertiesObject == null)
            return null;

        foreach (var propertyObject in EnumerateObject(
                     propertiesObject))
        {
            var name =
                GetString(
                    propertyObject,
                    "Name") ??
                GetString(
                    propertyObject,
                    "PropertyName") ??
                GetString(
                    propertyObject,
                    "Key") ??
                GetString(
                    propertyObject,
                    "TagName") ??
                GetString(
                    propertyObject,
                    "CanonicalName");

            if (!StringEqualsLoose(
                    name,
                    propertyName))
            {
                continue;
            }

            var value =
                GetPropertyValue(
                    propertyObject,
                    "Value") ??
                GetPropertyValue(
                    propertyObject,
                    "Data") ??
                GetPropertyValue(
                    propertyObject,
                    "PropertyValue");

            var date =
                ValueToDateTime(value);

            if (date.HasValue)
                return date;
        }

        return null;
    }

    private static IEnumerable<object> EnumerateObject(
        object? value)
    {
        if (value == null)
            yield break;

        if (value is string)
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

    private static string? ValueToString(
        object? value)
    {
        return value switch
        {
            null =>
                null,

            string text when
                string.IsNullOrWhiteSpace(text) =>
                null,

            string text =>
                text.Trim(),

            DateTime dateTime =>
                dateTime.ToString("O"),

            DateTimeOffset dateTimeOffset =>
                dateTimeOffset.ToString("O"),

            byte[] bytes when
                bytes.Length == 0 =>
                null,

            byte[] bytes =>
                Convert.ToBase64String(bytes),

            _ =>
                SafeToString(value)
        };
    }

    private static string? SafeToString(
        object value)
    {
        var text =
            value.ToString()?.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var type =
            value.GetType();

        if (text == type.FullName ||
            text == type.Name)
        {
            return null;
        }

        return text;
    }

    private static DateTime? ValueToDateTime(
        object? value)
    {
        return value switch
        {
            null =>
                null,

            DateTime dateTime =>
                dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(
                        dateTime,
                        DateTimeKind.Utc)
                    : dateTime.ToUniversalTime(),

            DateTimeOffset dateTimeOffset =>
                dateTimeOffset.UtcDateTime,

            string text when DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out var parsed) =>
                parsed,

            _ =>
                null
        };
    }

    private static string? NormalizeEmail(
        string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized =
            email.Trim();

        var match =
            Regex.Match(
                normalized,
                @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
                RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Value.ToLowerInvariant();
        }

        if (normalized.Contains('@'))
            return normalized.ToLowerInvariant();

        return null;
    }

    private static string? CleanNullable(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return NormalizeWhitespace(value);
    }

    private static string NormalizeWhitespace(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Regex.Replace(
            value,
            @"\s+",
            " ").Trim();
    }

    private static bool IsHtmlBody(
        string? bodyFormat,
        string body)
    {
        if (!string.IsNullOrWhiteSpace(bodyFormat) &&
            bodyFormat.Contains(
                "html",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return body.Contains(
                   "<html",
                   StringComparison.OrdinalIgnoreCase) ||
               body.Contains(
                   "<body",
                   StringComparison.OrdinalIgnoreCase) ||
               body.Contains(
                   "</",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string StripHtml(
        string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withoutScripts =
            Regex.Replace(
                html,
                "<script.*?</script>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        var withoutStyles =
            Regex.Replace(
                withoutScripts,
                "<style.*?</style>",
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        var withoutTags =
            Regex.Replace(
                withoutStyles,
                "<.*?>",
                " ",
                RegexOptions.Singleline);

        return WebUtility.HtmlDecode(
            withoutTags);
    }

    private static string SanitizeFileName(
        string fileName)
    {
        var invalidChars =
            Path.GetInvalidFileNameChars();

        var sanitized =
            new string(
                fileName
                    .Select(
                        character =>
                            invalidChars.Contains(
                                character)
                                ? '_'
                                : character)
                    .ToArray());

        sanitized =
            sanitized.Trim();

        return string.IsNullOrWhiteSpace(
            sanitized)
            ? $"attachment-{Guid.NewGuid():N}.bin"
            : sanitized;
    }

    private static string GuessContentType(
        string fileName)
    {
        var extension =
            Path.GetExtension(
                fileName)
                .ToLowerInvariant();

        return extension switch
        {
            ".txt" =>
                "text/plain",

            ".html" =>
                "text/html",

            ".htm" =>
                "text/html",

            ".pdf" =>
                "application/pdf",

            ".doc" =>
                "application/msword",

            ".docx" =>
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",

            ".xls" =>
                "application/vnd.ms-excel",

            ".xlsx" =>
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",

            ".csv" =>
                "text/csv",

            ".jpg" =>
                "image/jpeg",

            ".jpeg" =>
                "image/jpeg",

            ".png" =>
                "image/png",

            ".gif" =>
                "image/gif",

            ".zip" =>
                "application/zip",

            _ =>
                "application/octet-stream"
        };
    }

    private static bool StringEqualsLoose(
        string? left,
        string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return false;

        static string Normalize(
            string value)
        {
            return Regex.Replace(
                       value,
                       @"[^a-zA-Z0-9]",
                       string.Empty)
                .ToLowerInvariant();
        }

        return Normalize(left) ==
               Normalize(right);
    }

    private static string GetInnermostMessage(
        Exception exception)
    {
        var current =
            exception;

        while (current.InnerException != null)
            current =
                current.InnerException;

        return current.Message;
    }

    private static void TryDeleteFile(
        string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            /*
             * Ignore temp cleanup errors.
             */
        }
    }

    private static bool IsRtfBody(
        string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;

        return body
            .TrimStart()
            .StartsWith(
                @"{\rtf",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string ConvertRtfToPlainText(
        string rtf)
    {
        if (string.IsNullOrWhiteSpace(rtf))
            return string.Empty;

        var output =
            new StringBuilder();

        var ignoreStack =
            new Stack<bool>();

        var ignoreCurrentGroup =
            false;

        for (var i = 0;
             i < rtf.Length;
             i++)
        {
            var character =
                rtf[i];

            if (character == '{')
            {
                ignoreStack.Push(
                    ignoreCurrentGroup);

                continue;
            }

            if (character == '}')
            {
                ignoreCurrentGroup =
                    ignoreStack.Count > 0 &&
                    ignoreStack.Pop();

                continue;
            }

            if (character != '\\')
            {
                if (!ignoreCurrentGroup)
                    output.Append(character);

                continue;
            }

            if (i + 1 >= rtf.Length)
                continue;

            var next =
                rtf[i + 1];

            if (next is '\\' or '{' or '}')
            {
                if (!ignoreCurrentGroup)
                    output.Append(next);

                i++;

                continue;
            }

            if (next == '*')
            {
                ignoreCurrentGroup =
                    true;

                i++;

                continue;
            }

            if (next == '~')
            {
                if (!ignoreCurrentGroup)
                    output.Append(' ');

                i++;

                continue;
            }

            if (next == '-')
            {
                if (!ignoreCurrentGroup)
                    output.Append('-');

                i++;

                continue;
            }

            if (next == '_')
            {
                if (!ignoreCurrentGroup)
                    output.Append('-');

                i++;

                continue;
            }

            if (next == '\'')
            {
                if (i + 3 < rtf.Length)
                {
                    var hex =
                        rtf.Substring(
                            i + 2,
                            2);

                    if (byte.TryParse(
                            hex,
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out var value) &&
                        !ignoreCurrentGroup)
                    {
                        output.Append(
                            (char)value);
                    }

                    i += 3;
                }

                continue;
            }

            if (!char.IsLetter(next))
            {
                i++;
                continue;
            }

            var controlStart =
                i + 1;

            var cursor =
                controlStart;

            while (
                cursor < rtf.Length &&
                char.IsLetter(
                    rtf[cursor]))
            {
                cursor++;
            }

            var controlWord =
                rtf[
                    controlStart..cursor]
                    .ToLowerInvariant();

            var parameterStart =
                cursor;

            if (
                cursor < rtf.Length &&
                (rtf[cursor] == '-' ||
                 rtf[cursor] == '+'))
            {
                cursor++;
            }

            while (
                cursor < rtf.Length &&
                char.IsDigit(
                    rtf[cursor]))
            {
                cursor++;
            }

            var parameter =
                rtf[
                    parameterStart..cursor];

            if (
                cursor < rtf.Length &&
                rtf[cursor] == ' ')
            {
                /*
                 * RTF control-word delimiter.
                 */
            }
            else
            {
                cursor--;
            }

            HandleRtfControlWord(
                output,
                controlWord,
                parameter,
                ref ignoreCurrentGroup);

            i =
                cursor;
        }

        var text =
            output.ToString();

        text =
            Regex.Replace(
                text,
                @"\r\n|\r|\n",
                "\n");

        text =
            Regex.Replace(
                text,
                @"[ \t]+",
                " ");

        text =
            Regex.Replace(
                text,
                @"\n\s+",
                "\n");

        text =
            Regex.Replace(
                text,
                @"(\n\s*){3,}",
                "\n\n");

        return NormalizeWhitespace(
            text);
    }

    private static void HandleRtfControlWord(
        StringBuilder output,
        string controlWord,
        string parameter,
        ref bool ignoreCurrentGroup)
    {
        if (IsIgnoredRtfDestination(
                controlWord))
        {
            ignoreCurrentGroup =
                true;

            return;
        }

        if (ignoreCurrentGroup)
            return;

        switch (controlWord)
        {
            case "par":
            case "line":
                output.AppendLine();
                return;

            case "tab":
                output.Append('\t');
                return;

            case "emdash":
                output.Append('—');
                return;

            case "endash":
                output.Append('–');
                return;

            case "bullet":
                output.Append('•');
                return;

            case "lquote":
                output.Append('‘');
                return;

            case "rquote":
                output.Append('’');
                return;

            case "ldblquote":
                output.Append('“');
                return;

            case "rdblquote":
                output.Append('”');
                return;

            case "u":
                AppendUnicodeCharacter(
                    output,
                    parameter);

                return;
        }
    }

    private static bool IsIgnoredRtfDestination(
        string controlWord)
    {
        return controlWord is
            "fonttbl" or
            "colortbl" or
            "stylesheet" or
            "info" or
            "pict" or
            "object" or
            "generator" or
            "xmlnstbl" or
            "datastore" or
            "themedata" or
            "colorschememapping";
    }

    private static void AppendUnicodeCharacter(
        StringBuilder output,
        string parameter)
    {
        if (!int.TryParse(
                parameter,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return;
        }

        if (value < 0)
            value += 65536;

        try
        {
            output.Append(
                char.ConvertFromUtf32(
                    value));
        }
        catch
        {
            /*
             * Ignore invalid unicode escape.
             */
        }
    }

    private static void ValidatePstHeader(
        string pstFilePath)
    {
        const int headerLength = 4;

        var fileInfo =
            new FileInfo(
                pstFilePath);

        if (fileInfo.Length < headerLength)
        {
            throw new InvalidDataException(
                "InvalidPstFile: file is too small to be a valid PST/OST file.");
        }

        var header =
            new byte[headerLength];

        using var stream =
            File.OpenRead(
                pstFilePath);

        var bytesRead =
            stream.Read(
                header,
                0,
                header.Length);

        if (bytesRead < headerLength)
        {
            throw new InvalidDataException(
                "InvalidPstFile: could not read PST/OST file header.");
        }

        /*
         * PST/OST files start with:
         *
         * 21 42 44 4E = !BDN
         */
        var hasXstMagicHeader =
            header[0] == 0x21 &&
            header[1] == 0x42 &&
            header[2] == 0x44 &&
            header[3] == 0x4E;

        if (!hasXstMagicHeader)
        {
            throw new InvalidDataException(
                "InvalidPstFile: missing PST/OST magic header.");
        }
    }

    private sealed record BodyExtractionResult(
        string? BodyText,
        string? BodyHtml);
}
