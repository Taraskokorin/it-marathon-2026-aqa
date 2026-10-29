using System.Text.Json.Nodes;

namespace GreenTasty.E2E.Support;

/// <summary>Registered user (Owner, other client or waiter) with a Cognito id token.</summary>
public sealed record User(string FirstName, string LastName, string Email, string Password, string Token);

/// <summary>Anonymous guest who joined a group by invitation.</summary>
public sealed record Guest(string Name, string SessionId, string SessionToken);

public sealed record Dish(string Id, string Name, decimal Price);

/// <summary>Location with one table, a menu and an assigned waiter, created for one scenario.</summary>
public sealed record Restaurant(string LocationId, string Address, string TableId, int TableCapacity, IReadOnlyList<Dish> Menu, User Waiter)
{
    public Dish DishNamed(string name) =>
        Menu.FirstOrDefault(d => d.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Dish '{name}' is not on the test menu: {string.Join(", ", Menu.Select(d => d.Name))}");
}

/// <summary>Owner's reservation that has a group pre-order.</summary>
public sealed record GroupBooking(string ReservationId, string GroupReservationId, string InviteToken);

/// <summary>One row of GET /guest/groups/{id}/sessions.</summary>
public sealed record ParticipantView(string? Id, string Type, string Name, decimal Total, bool IsSubmitted, IReadOnlyDictionary<string, int> Items)
{
    public static ParticipantView From(JsonNode node) => new(
        node["id"]?.GetValue<string>(),
        node["type"]?.ToString() ?? "",
        node["name"]?.ToString() ?? "",
        node["total"]?.GetValue<decimal>() ?? 0,
        node["isSubmitted"]?.GetValue<bool>() ?? false,
        (node["items"]?.AsArray() ?? [])
            .ToDictionary(i => i!["dish"]!["name"]!.ToString(), i => i!["quantity"]!.GetValue<int>()));
}

public static class Json
{
    /// <summary>Reads a string property ignoring the case of its name (handlers differ in naming).</summary>
    public static string Str(JsonNode? node, string name)
    {
        if (node is JsonObject obj)
            foreach (var (key, value) in obj)
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && value is not null)
                    return value.ToString();

        throw new InvalidOperationException($"Property '{name}' not found in {node?.ToJsonString()}");
    }
}
