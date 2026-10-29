using GreenTasty.E2E.Api;

namespace GreenTasty.E2E.Support;

/// <summary>
/// Everything one scenario created or observed. Injected into step classes by Reqnroll (one instance per scenario),
/// so scenarios never share data and can run in parallel.
/// </summary>
public sealed class ScenarioState
{
    public Restaurant? Restaurant { get; set; }
    public User? Owner { get; set; }
    public GroupBooking? Booking { get; set; }

    public Dictionary<string, User> Clients { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Guest> Guests { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Last API response checked by "Then the request is rejected ..." steps.</summary>
    public ApiResponse? LastResponse { get; set; }

    /// <summary>Clean-up actions, executed in reverse order after the scenario.</summary>
    public Stack<(string What, Func<Task<ApiResponse>> Action)> Cleanup { get; } = new();

    public Restaurant RequireRestaurant() => Restaurant ?? throw new InvalidOperationException("No restaurant was set up.");
    public User RequireOwner() => Owner ?? throw new InvalidOperationException("No Owner in this scenario.");
    public GroupBooking RequireBooking() => Booking ?? throw new InvalidOperationException("The Owner has no group booking yet.");

    public Guest Guest(string name) =>
        Guests.TryGetValue(name, out var guest) ? guest : throw new ArgumentException($"Unknown guest '{name}'.");
}
