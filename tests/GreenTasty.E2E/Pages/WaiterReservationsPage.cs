using Microsoft.Playwright;

namespace GreenTasty.E2E.Pages;

/// <summary>Waiter dashboard (/waiter-reservations) and the pre-order page opened from a reservation card.</summary>
public sealed class WaiterReservationsPage(IPage page)
{
    /// <summary>Each scenario has its own location, so the address identifies the reservation card.</summary>
    public ILocator ReservationCard(string address) => page.Locator("li.card").Filter(new() { HasText = address });

    public ILocator PreOrderDishes => page.Locator(".preorder-list .pre-order-dish");

    public async Task Open()
    {
        await page.GotoAsync("/waiter-reservations");
    }

    public async Task OpenPreOrder(string address)
    {
        var card = ReservationCard(address);
        await card.WaitForAsync();
        await card.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/waiter-pre-order**");
    }

    public ILocator Dish(string dishName) => PreOrderDishes.Filter(new() { HasText = dishName });

    public ILocator Quantity(string dishName) => Dish(dishName).Locator(".pre-order-quantity");
}
