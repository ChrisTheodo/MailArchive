using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MailArchive.Tests
{
    public class BaseTest : PageTest
    {
        protected async Task LoginAsync()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator emailField = Page.GetByRole(
                AriaRole.Textbox,
                new PageGetByRoleOptions { Name = "Email" }
            );

            ILocator passwordField = Page.GetByLabel(
                "Κωδικός πρόσβασης",
                new PageGetByLabelOptions { Exact = true }
            );

            await emailField.FillAsync("user@example.com");
            await passwordField.FillAsync("User123!");

            ILocator loginButton = Page.GetByRole(
                AriaRole.Button,
                new PageGetByRoleOptions
                {
                    Name = "Σύνδεση",
                    Exact = true
                }
            );

            await loginButton.ClickAsync();

            await Page.WaitForURLAsync(
                url => !url.ToString().Equals("http://localhost:5173/")
            );
        }
    }
}