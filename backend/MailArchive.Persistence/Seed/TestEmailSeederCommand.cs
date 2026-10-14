using Microsoft.Extensions.DependencyInjection;

namespace MailArchive.Persistence.Seed;

/// <summary>
/// Command-line entry point for <see cref="TestEmailSeeder"/>. Call it from Program.cs after
/// the app is built; it returns true when it handled a seed command, so the app should exit:
///
///   if (await TestEmailSeederCommand.TryRunAsync(app.Services, args, app.Environment.ContentRootPath))
///       return;
///
/// Commands:
///   --seed-test-emails user@example.com [--mailbox "User Archive"] [--count 200]
///   --remove-test-emails user@example.com [--mailbox "User Archive"]
/// </summary>
public static class TestEmailSeederCommand
{
    public static async Task<bool> TryRunAsync(IServiceProvider services, string[] args, string contentRootPath)
    {
        var seedFor = GetArg(args, "--seed-test-emails");
        var removeFor = GetArg(args, "--remove-test-emails");

        if (seedFor == null && removeFor == null)
            return false;

        var mailbox = GetArg(args, "--mailbox");
        var countArg = GetArg(args, "--count");
        int? count = null;

        if (countArg != null)
        {
            if (!int.TryParse(countArg, out var parsed))
                throw new ArgumentException($"--count must be a number, got '{countArg}'.");

            count = parsed;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MailArchiveDbContext>();

        if (seedFor != null)
        {
            var added = await TestEmailSeeder.SeedForUserAsync(db, seedFor, contentRootPath, mailbox, count);
            Console.WriteLine($"Added {added} test emails for {seedFor}.");
        }
        else
        {
            var removed = await TestEmailSeeder.RemoveForUserAsync(db, removeFor!, mailbox);
            Console.WriteLine($"Removed {removed} test emails for {removeFor}.");
        }

        return true;
    }

    private static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
