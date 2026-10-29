namespace GreenTasty.E2E.Support;

/// <summary>Unique, run-specific test data so that parallel runs never collide.</summary>
public static class TestData
{
    private static readonly string RunId = DateTime.UtcNow.ToString("MMddHHmm");

    public static string Unique() => Guid.NewGuid().ToString("N")[..8];

    public static string Email(string role) => $"gt-aqa-{role}-{RunId}-{Unique()}@example.com";

    /// <summary>Names: Latin letters only (backend rule), unique so that UI cards can be told apart.</summary>
    public static string Name(string baseName) => baseName + Letters(4);

    public static string Address() => $"{Unique()} Test Avenue";

    /// <summary>Reservation slot two days ahead: far from the 15-minute rule and from midnight edge cases.</summary>
    public static (DateTime Start, DateTime End) Slot()
    {
        var start = DateTime.UtcNow.Date.AddDays(2).AddHours(12);
        return (start, start.AddMinutes(90));
    }

    private static string Letters(int count)
    {
        const string abc = "abcdefghijklmnopqrstuvwxyz";
        return new string(Enumerable.Range(0, count).Select(_ => abc[Random.Shared.Next(abc.Length)]).ToArray());
    }
}
