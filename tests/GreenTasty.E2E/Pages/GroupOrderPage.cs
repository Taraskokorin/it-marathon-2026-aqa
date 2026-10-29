using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GreenTasty.E2E.Pages;

/// <summary>Owner screen of the group pre-order: /reservations/{reservationId}/group-order (Frontend homework #6).</summary>
public sealed partial class GroupOrderPage(IPage page)
{
    public ILocator Heading => page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true });
    public ILocator StatusBadge => page.Locator(".status-badge");
    public ILocator GrandTotal => page.Locator(".grand-total .amount");
    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order" });
    public ILocator SentButton => page.GetByRole(AriaRole.Button, new() { Name = "Sent to Restaurant" });
    public ILocator ConsolidatedItems => page.Locator("ul.consolidated li");
    public ILocator NotFound => page.GetByRole(AriaRole.Heading, new() { Name = "Group reservation not found" });
    public ILocator ParticipantCards => page.Locator("ul.participants article.card");

    public async Task Open(string reservationId)
    {
        await page.GotoAsync($"/reservations/{reservationId}/group-order");
        await Heading.WaitForAsync();
    }

    /// <summary>Card titled "Guest: {name}" or "Owner: {name}".</summary>
    public ILocator Card(string participantName) =>
        ParticipantCards.Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex($": {Regex.Escape(participantName)}$") }) });

    public ILocator CardChip(string participantName) => Card(participantName).Locator(".chip");

    public ILocator CardItem(string participantName, string dishName) =>
        Card(participantName).Locator("li.item").Filter(new() { HasText = dishName });

    public async Task<decimal> Subtotal(string participantName) =>
        Money(await Card(participantName).Locator(".subtotal span").Last.InnerTextAsync());

    public async Task<decimal> Total() => Money(await GrandTotal.InnerTextAsync());

    /// <summary>The page polls the API every 15 s; the Refresh button forces a reload of the data.</summary>
    public async Task Refresh()
    {
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true });
        if (await refresh.IsVisibleAsync()) await refresh.ClickAsync();
    }

    public async Task Submit()
    {
        await SubmitButton.ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
    }

    public async Task Remove(string participantName)
    {
        await Card(participantName).GetByRole(AriaRole.Button, new() { Name = $"Remove {participantName}" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Remove Guest" }).ClickAsync();
    }

    /// <summary>"$1,234.50" -> 1234.50 (Angular currency pipe, en-US).</summary>
    public static decimal Money(string text) =>
        decimal.Parse(MoneyRegex().Match(text).Value.Replace(",", ""), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\d[\d,]*(\.\d+)?")]
    private static partial Regex MoneyRegex();
}
