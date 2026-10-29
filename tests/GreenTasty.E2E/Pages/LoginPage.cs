using Microsoft.Playwright;

namespace GreenTasty.E2E.Pages;

public sealed class LoginPage(IPage page)
{
    private ILocator Email => page.GetByPlaceholder("e.g. username@domain.com");
    private ILocator Password => page.Locator("input[formcontrolname='password']");
    private ILocator SignInButton => page.GetByRole(AriaRole.Button, new() { Name = "Sign In", Exact = true });

    public async Task SignIn(string email, string password)
    {
        await page.GotoAsync("/login");
        await Email.FillAsync(email);
        await Password.FillAsync(password);
        await SignInButton.ClickAsync();

        // A successful sign-in always leaves /login (client -> /main, waiter -> /waiter-reservations).
        await page.WaitForURLAsync(url => !url.Contains("/login"));
    }
}
