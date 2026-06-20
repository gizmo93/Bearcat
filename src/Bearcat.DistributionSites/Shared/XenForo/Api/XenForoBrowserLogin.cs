using Bearcat.Abstractions.DistributionSite.Dto;
using Bearcat.DistributionSites.Shared;
using Microsoft.Playwright;

namespace Bearcat.DistributionSites.Shared.XenForo.Api;

public static class XenForoBrowserLogin
{
    private const string UserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    public static async Task<DistributionSession?> LoginAsync(
        string baseUrl,
        string username,
        string password
    )
    {
        PlaywrightBrowsers.Ensure();

        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true }
        );

        await using var context = await browser.NewContextAsync(
            new BrowserNewContextOptions { UserAgent = UserAgent, Locale = "de-DE" }
        );

        var page = await context.NewPageAsync();

        await page.GotoAsync(
            url: baseUrl,
            options: new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
        );

        await OpenLoginModalAsync(page);

        await page.FillAsync("input[name='login']", username);
        await page.FillAsync("input[name='password']", password);

        var rememberMe = page.Locator("input[name='remember']");
        if (await rememberMe.CountAsync() > 0)
        {
            await rememberMe.First.CheckAsync();
        }

        await page.ClickAsync(
            ".overlay button.button--icon--login, .overlay button[type='submit'], "
                + "form[action*='login'] button[type='submit']"
        );

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (!await IsLoggedInAsync(page))
        {
            return null;
        }

        var cookies = await context.CookiesAsync();

        var sessionCookies = cookies
            .Select(cookie => new SessionCookie(
                Name: cookie.Name,
                Value: cookie.Value,
                Domain: cookie.Domain,
                Path: cookie.Path
            ))
            .ToList();

        return new DistributionSession(UserAgent, sessionCookies);
    }

    private static async Task OpenLoginModalAsync(IPage page)
    {
        var loginInput = page.Locator("input[name='login']");

        if (await loginInput.CountAsync() > 0 && await loginInput.First.IsVisibleAsync())
        {
            return;
        }

        var loginTrigger = page.Locator(
            "a.p-navgroup-link--logIn, a[href='/login/'], a[href$='/login/'], a[data-xf-click='overlay']"
        );
        await loginTrigger.First.ClickAsync();

        await page.WaitForSelectorAsync(
            selector: "input[name='login']",
            options: new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15_000,
            }
        );
    }

    private static async Task<bool> IsLoggedInAsync(IPage page)
    {
        var accountMenu = page.Locator(
            ".p-navgroup-link--user, [data-visitormenu], a[href*='/account/'], .p-navgroup--member"
        );
        if (await accountMenu.CountAsync() > 0)
        {
            return true;
        }

        var loginLink = page.Locator("a.p-navgroup-link--logIn, a[href='/login/']");
        return await loginLink.CountAsync() == 0;
    }
}
