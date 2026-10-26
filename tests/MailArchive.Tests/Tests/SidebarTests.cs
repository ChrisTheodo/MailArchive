using Microsoft.Playwright;
using NUnit.Framework;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MailArchive.Tests.Tests
{
    public class SidebarTests : BaseTest
    {
        [Test]
        public async Task Sidebar_Should_Open_WhenHamburgerIsClicked()
        {
            await LoginAsync();

            ILocator hamburgerButton = Page
                .GetByRole(AriaRole.Banner)
                .Locator("button")
                .Filter(new LocatorFilterOptions
                {
                    Has = Page.Locator("i.i-mdi\\:menu")
                });

            await Expect(hamburgerButton).ToBeVisibleAsync();

            await hamburgerButton.ClickAsync();

            ILocator sidebar = Page.Locator(
                "nav.v-navigation-drawer"
            );

            await Expect(sidebar).ToBeVisibleAsync();
        }

        [Test]
        public async Task Sidebar_Should_Expand_WhenHovered()
        {
            await LoginAsync();

            ILocator sidebar = Page.Locator(
                "nav.v-navigation-drawer"
            );

            await Expect(sidebar).ToBeVisibleAsync();

            await Expect(sidebar).ToHaveClassAsync(
                new Regex(
                    @"v-navigation-drawer--expand-on-hover"
                )
            );

            await sidebar.HoverAsync();

            await Expect(sidebar).ToHaveClassAsync(
                new Regex(
                    @"v-navigation-drawer--is-hovering"
                )
            );
        }
    }
}