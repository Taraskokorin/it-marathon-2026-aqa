using GreenTasty.E2E.Pages;
using GreenTasty.E2E.Support;
using Microsoft.Playwright;
using Reqnroll;
using static Microsoft.Playwright.Assertions;

namespace GreenTasty.E2E.Steps;

/// <summary>Main scenarios through the user interface: what the Owner and the waiter actually see.</summary>
[Binding]
public sealed class GroupOrderUiSteps(IPage page, LoginPage login, GroupOrderPage groupOrder, WaiterReservationsPage waiter, ScenarioState state)
{
    private string? _signedIn;

    [Given(@"^the Owner opens the group pre-order page$")]
    [When(@"^the Owner opens the group pre-order page$")]
    public async Task OwnerOpensPage()
    {
        await SignInAs(state.RequireOwner());
        await groupOrder.Open(state.RequireBooking().ReservationId);
        await Expect(groupOrder.StatusBadge).ToBeVisibleAsync();
    }

    [When(@"^the Owner opens the group pre-order page of a reservation that does not exist$")]
    public async Task OwnerOpensMissing()
    {
        await SignInAs(state.RequireOwner());
        await groupOrder.Open(Guid.NewGuid().ToString());
    }

    [When(@"^the Owner refreshes the group pre-order page$")]
    public Task OwnerRefreshes() => groupOrder.Refresh();

    [When(@"^the Owner submits the pre-order on the page$")]
    public Task OwnerSubmits() => groupOrder.Submit();

    [When(@"^the Owner removes participant ""([^""]+)"" on the page$")]
    public Task OwnerRemoves(string guest) => groupOrder.Remove(state.Guest(guest).Name);

    [When(@"^the waiter opens the pre-order of the reservation$")]
    public async Task WaiterOpensPreOrder()
    {
        var restaurant = state.RequireRestaurant();
        await SignInAs(restaurant.Waiter);
        await waiter.Open();
        await waiter.OpenPreOrder(restaurant.Address);
    }

    // ---------- Then: Owner page ----------

    [Then(@"^the page says that the group reservation was not found$")]
    public Task ThenNotFound() => Expect(groupOrder.NotFound).ToBeVisibleAsync();

    [Given(@"^the page shows grand total (\d+\.\d+)$")]
    [Then(@"^the page shows grand total (\d+\.\d+)$")]
    public Task ThenGrandTotal(decimal total) =>
        Expect(groupOrder.GrandTotal).ToHaveTextAsync(Money(total));

    [Then(@"^the page shows participant ""([^""]+)"" as ""(Submitted|Still choosing)"" with subtotal (\d+\.\d+)$")]
    public async Task ThenParticipantCard(string guest, string chip, decimal subtotal)
    {
        var name = state.Guest(guest).Name;
        await Expect(groupOrder.Card(name)).ToBeVisibleAsync();
        await Expect(groupOrder.CardChip(name)).ToHaveTextAsync(chip);
        await Expect(groupOrder.Card(name).Locator(".subtotal")).ToContainTextAsync(Money(subtotal));
    }

    [Then(@"^participant ""([^""]+)"" has (\d+) x ""([^""]+)""$")]
    public async Task ThenParticipantItem(string guest, int quantity, string dish)
    {
        var item = groupOrder.CardItem(state.Guest(guest).Name, DishName(dish));
        await Expect(item.Locator(".qty")).ToHaveTextAsync($"× {quantity}");
    }

    [Then(@"^the page does not show participant ""([^""]+)""$")]
    public Task ThenNoCard(string guest) => Expect(groupOrder.Card(state.Guest(guest).Name)).ToHaveCountAsync(0);

    [Then(@"^the consolidated pre-order lists (\d+) x ""([^""]+)""$")]
    public Task ThenConsolidated(int quantity, string dish) =>
        Expect(groupOrder.ConsolidatedItems.Filter(new() { HasText = DishName(dish) }))
            .ToContainTextAsync($"{DishName(dish)} × {quantity}");

    [Then(@"^the consolidated pre-order does not list ""([^""]+)""$")]
    public Task ThenNotConsolidated(string dish) =>
        Expect(groupOrder.ConsolidatedItems.Filter(new() { HasText = DishName(dish) })).ToHaveCountAsync(0);

    [Then(@"^the page shows that the pre-order was sent to the restaurant$")]
    public async Task ThenSent()
    {
        await Expect(groupOrder.SentButton).ToBeVisibleAsync();
        await Expect(groupOrder.SentButton).ToBeDisabledAsync();
        await Expect(groupOrder.SubmitButton).ToHaveCountAsync(0);
    }

    // ---------- Then: waiter ----------

    [Then(@"^the waiter sees (\d+) x ""([^""]+)""$")]
    public Task ThenWaiterSees(int quantity, string dish) =>
        Expect(waiter.Quantity(DishName(dish))).ToHaveTextAsync($"Quantity: {quantity}");

    [Then(@"^the waiter does not see ""([^""]+)""$")]
    public Task ThenWaiterDoesNotSee(string dish) => Expect(waiter.Dish(DishName(dish))).ToHaveCountAsync(0);

    // ---------- helpers ----------

    /// <summary>Signs in through the login form; switching users clears the previous session first.</summary>
    private async Task SignInAs(User user)
    {
        if (_signedIn == user.Email) return;

        if (_signedIn is not null)
        {
            await page.Context.ClearCookiesAsync();
            await page.EvaluateAsync("() => { localStorage.clear(); sessionStorage.clear(); }");
        }

        await login.SignIn(user.Email, user.Password);
        _signedIn = user.Email;
    }

    private string DishName(string dish) => state.RequireRestaurant().DishNamed(dish).Name;

    /// <summary>Angular currency pipe output for en-US: $1,234.50</summary>
    private static string Money(decimal amount) => amount.ToString("$#,##0.00", System.Globalization.CultureInfo.InvariantCulture);
}
