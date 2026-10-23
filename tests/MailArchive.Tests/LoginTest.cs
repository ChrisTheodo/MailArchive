using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System.Threading.Tasks;

namespace MailArchive.Tests
{
    public class LoginTests : PageTest
    {
        [Test]
        public async Task LoginPage_Should_DisplayEmailAndPasswordFields()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator emailField = Page.GetByRole( AriaRole.Textbox, new PageGetByRoleOptions { Name = "Email" });

            ILocator passwordField = Page.GetByLabel("Κωδικός πρόσβασης",new PageGetByLabelOptions { Exact = true });

            await Expect(emailField).ToBeVisibleAsync();
            await Expect(passwordField).ToBeVisibleAsync();
        }

        [Test]
        public async Task LoginPage_Should_DisplayLoginButton()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator loginButton = Page.GetByRole(AriaRole.Button,new PageGetByRoleOptions { Name = "Σύνδεση", Exact = true });

            await Expect(loginButton).ToBeVisibleAsync();
            await Expect(loginButton).ToBeEnabledAsync();
        }

        [Test]
        public async Task LoginPage_Should_ShowRequiredFieldErrors_WhenFieldsAreEmpty()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator loginButton = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Σύνδεση", Exact = true });

            await loginButton.ClickAsync();

            ILocator requiredMessages = Page.GetByText("Υποχρεωτικό πεδίο", new PageGetByTextOptions { Exact = true });

            await Expect(requiredMessages.First).ToBeVisibleAsync();
            await Expect(requiredMessages).ToHaveCountAsync(2);
        }

        [Test]
        public async Task LoginPage_Should_ShowError_WhenCredentialsAreInvalid()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator emailField = Page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = "Email" });

            ILocator passwordField = Page.GetByLabel("Κωδικός πρόσβασης",new PageGetByLabelOptions { Exact = true });

            await emailField.FillAsync("wrong@example.com");
            await passwordField.FillAsync("LATHOSKODIKOS");

            ILocator loginButton = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Σύνδεση", Exact = true });

            await loginButton.ClickAsync();

            ILocator errorMessage = Page.GetByText("Λάθος email ή κωδικός πρόσβασης", new PageGetByTextOptions { Exact = true });

            await Expect(errorMessage).ToBeVisibleAsync();
        }

        [Test]
        public async Task LoginPage_Should_LoginSuccessfully_WithValidCredentials()
        {
            await Page.GotoAsync("http://localhost:5173");

            ILocator emailField = Page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = "Email" });

            ILocator passwordField = Page.GetByLabel("Κωδικός πρόσβασης", new PageGetByLabelOptions { Exact = true });

            await emailField.FillAsync("user@example.com");
            await passwordField.FillAsync("User123!");

            ILocator loginButton = Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Σύνδεση", Exact = true });

            await loginButton.ClickAsync();

            await Expect(Page).Not.ToHaveURLAsync("http://localhost:5173/");
        }
    }
}