using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace Infrastructure.Browser;

public class BrowserHandler : IBrowserHandler
{
    public async Task<string> BuildImageFromHtml(string html, string target)
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Args = ["--no-sandbox", "--disable-setuid-sandbox"] }
        );
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        var imageBytes = await page.Locator(target).ScreenshotAsync();
        var imageBase64 = Convert.ToBase64String(imageBytes);

        return imageBase64;
    }
}
