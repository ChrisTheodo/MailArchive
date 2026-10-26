using Microsoft.Playwright;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MailArchive.Tests;

public class SearchTests : BaseTest
{
    [Test]
    public async Task Search_Should_AcceptSearchTerm()
    {
        await LoginAsync();

        ILocator searchField = Page.GetByPlaceholder(
            "Αναζήτηση email",
            new PageGetByPlaceholderOptions{Exact = true}
        );

        await Expect(searchField).ToBeVisibleAsync();

        await searchField.ClickAsync();
        await searchField.FillAsync("travel");

        await Expect(searchField).ToHaveValueAsync("travel");
    }

    [Test]
    public async Task Search_Should_ShowResults_WhenTyping()
    {
        await LoginAsync();

        ILocator searchField = Page.GetByPlaceholder(
            "Αναζήτηση email",
            new PageGetByPlaceholderOptions{Exact = true}
        );

        await searchField.ClickAsync();
        await searchField.FillAsync("travel");

        ILocator results = Page.Locator("div.v-list-item");

        await Expect(results.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Search_Should_ShowMatchingEmail()
    {
        await LoginAsync();

        ILocator searchField = Page.GetByPlaceholder(
            "Αναζήτηση email",
            new PageGetByPlaceholderOptions
            {
                Exact = true
            }
        );

        await searchField.ClickAsync();
        await searchField.FillAsync("travel");

        ILocator matchingEmail = Page
            .Locator(".mail-row")
            .Filter(new LocatorFilterOptions
            {
                Has = Page.Locator(".subject").GetByText(
                    "Travel documents",
                    new LocatorGetByTextOptions
                    {
                        Exact = true
                    }
                )
            });

        await Expect(matchingEmail).ToBeVisibleAsync();

        await Expect(matchingEmail.Locator(".subject"))
            .ToHaveTextAsync("Travel documents");
    }

    [Test]
    public async Task Search_ShouldShowOnlyMatchingEmails()
    {
        await LoginAsync();

        ILocator searchField = Page.GetByPlaceholder(
            "Αναζήτηση email",
            new PageGetByPlaceholderOptions
            {
                Exact = true
            }
        );

        await Expect(searchField).ToBeVisibleAsync();

        await searchField.FillAsync("travel");

        ILocator emailRows = Page.Locator(".mail-row");

        await Expect(emailRows).ToHaveCountAsync(1);

        await Expect(
            emailRows.First.Locator(".subject")
        ).ToHaveTextAsync("Travel documents");
    }

    [Test]
    public async Task Search_ShouldShowNoResults_WhenNoEmailMatches()
    {
        await LoginAsync();

        ILocator searchField = Page.GetByPlaceholder(
            "Αναζήτηση email",
            new PageGetByPlaceholderOptions
            {
                Exact = true
            }
        );

        await searchField.FillAsync("asdgasdgasdg");

        ILocator emailRows = Page.Locator(".mail-row");

        await Expect(emailRows).ToHaveCountAsync(0);
    }
}