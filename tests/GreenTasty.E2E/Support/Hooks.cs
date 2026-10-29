using GreenTasty.E2E.Config;
using Microsoft.Playwright;
using Reqnroll;
using Reqnroll.BoDi;

namespace GreenTasty.E2E.Support;

[Binding]
public sealed class Hooks(IObjectContainer container, ScenarioContext scenario, ScenarioState state)
{
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;
    private static readonly SemaphoreSlim BrowserLock = new(1, 1);

    private IBrowserContext? _context;
    private string? _tracePath;

    /// <summary>One browser per test run, a fresh isolated context per UI scenario.</summary>
    [BeforeScenario("ui", Order = 0)]
    public async Task StartBrowser()
    {
        await BrowserLock.WaitAsync();
        try
        {
            _playwright ??= await Playwright.CreateAsync();
            _browser ??= await _playwright.Chromium.LaunchAsync(new() { Headless = Settings.Headless, SlowMo = Settings.SlowMo });
        }
        finally
        {
            BrowserLock.Release();
        }

        var name = Safe(scenario.ScenarioInfo.Title);
        Directory.CreateDirectory(Settings.ArtifactsDir);
        _tracePath = Path.Combine(Settings.ArtifactsDir, $"{name}.trace.zip");

        _context = await _browser.NewContextAsync(new()
        {
            BaseURL = Settings.AppUrl,
            ViewportSize = new() { Width = 1440, Height = 1000 },
            RecordVideoDir = Path.Combine(Settings.ArtifactsDir, "videos", name),
        });
        _context.SetDefaultTimeout(20_000);
        await _context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true, Title = scenario.ScenarioInfo.Title });

        container.RegisterInstanceAs(await _context.NewPageAsync());
    }

    [AfterScenario("ui", Order = 0)]
    public async Task StopBrowser()
    {
        if (_context is null) return;

        var failed = scenario.TestError is not null;
        if (failed)
        {
            var page = container.Resolve<IPage>();
            var shot = Path.ChangeExtension(_tracePath!, ".png");
            await page.ScreenshotAsync(new() { Path = shot, FullPage = true });
            TestContext.AddTestAttachment(shot, "Screenshot of the failure");
        }

        // Trace and video are kept only for failed scenarios to keep the report small.
        await _context.Tracing.StopAsync(new() { Path = failed ? _tracePath : null });
        var video = container.Resolve<IPage>().Video;
        await _context.CloseAsync();

        if (failed)
        {
            TestContext.AddTestAttachment(_tracePath!, "Playwright trace (npx playwright show-trace)");
            if (video is not null) TestContext.AddTestAttachment(await video.PathAsync(), "Video");
        }
        else if (video is not null)
        {
            await video.DeleteAsync();
        }
    }

    /// <summary>Removes everything the scenario created, even when it failed. Errors are logged, not thrown.</summary>
    [AfterScenario(Order = 100)]
    public async Task CleanUp()
    {
        while (state.Cleanup.TryPop(out var item))
        {
            try
            {
                var response = await item.Action();
                if (!response.IsSuccess)
                    TestContext.Progress.WriteLine($"  clean-up '{item.What}' returned {response}");
            }
            catch (Exception e)
            {
                TestContext.Progress.WriteLine($"  clean-up '{item.What}' failed: {e.Message}");
            }
        }
    }

    [AfterTestRun]
    public static async Task CloseBrowser()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private static string Safe(string title)
    {
        var name = string.Concat(title.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        return name[..Math.Min(80, name.Length)];
    }
}
