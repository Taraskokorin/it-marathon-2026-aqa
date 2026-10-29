namespace GreenTasty.E2E.Config;

/// <summary>
/// Test configuration. Values come from environment variables; locally they can be put into a .env file
/// (see .env.example). Nothing environment-specific is committed to the repository.
/// </summary>
public static class Settings
{
    static Settings() => DotEnv.Load();

    /// <summary>Deployed frontend, e.g. http://my-bucket.s3-website.eu-north-1.amazonaws.com</summary>
    public static string AppUrl => Required("APP_URL").TrimEnd('/');

    /// <summary>Deployed API Gateway stage, e.g. https://xxxx.execute-api.eu-north-1.amazonaws.com/develop</summary>
    public static string ApiUrl => Required("API_URL").TrimEnd('/');

    /// <summary>Password used for every user the tests register (8–16 characters).</summary>
    public static string TestPassword => Optional("TEST_PASSWORD") ?? "Gt-Test-2026!";

    public static bool Headless => !string.Equals(Optional("HEADED"), "true", StringComparison.OrdinalIgnoreCase);

    public static float SlowMo => float.TryParse(Optional("SLOWMO"), out var ms) ? ms : 0;

    /// <summary>Where traces, screenshots and videos of failed UI scenarios are written.</summary>
    public static string ArtifactsDir => Optional("ARTIFACTS_DIR") ?? Path.Combine(AppContext.BaseDirectory, "artifacts");

    private static string Required(string name) =>
        Optional(name) ?? throw new InvalidOperationException(
            $"Environment variable {name} is not set. Copy .env.example to .env and fill it in, or set it in CI.");

    private static string? Optional(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>Minimal .env reader: KEY=VALUE lines, # comments. Real environment variables win.</summary>
internal static class DotEnv
{
    public static void Load()
    {
        var file = FindUp(".env");
        if (file is null) return;

        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim().Trim('"');
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindUp(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
