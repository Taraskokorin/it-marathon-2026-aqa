using GreenTasty.E2E.Api;
using GreenTasty.E2E.Support;
using Reqnroll;

namespace GreenTasty.E2E.Steps;

/// <summary>Data preparation and API-level checks (authorisation, validation, state after actions).</summary>
[Binding]
public sealed class GroupOrderApiSteps(GroupOrderTasks tasks, RestaurantApi api, ScenarioState state)
{
    private const string MissingId = "00000000-0000-0000-0000-000000000000";

    // ---------- Given: test data ----------

    [Given(@"^a restaurant with a table for (\d+) guests$")]
    public Task GivenRestaurant(int capacity) => tasks.SetUpRestaurant(capacity);

    [Given(@"^a client ""Owner"" has booked the table for (\d+) guests$")]
    public async Task GivenOwnerBooked(int guests)
    {
        state.Owner = await tasks.RegisterUser("owner", "Owner");
        await tasks.BookForGroup(state.Owner, guests);
    }

    [Given(@"^guest ""([^""]+)"" has joined the group$")]
    public Task GivenGuestJoined(string guest) => tasks.Join(guest);

    [Given(@"^""([^""]+)"" has added (\d+) x ""([^""]+)"" to the cart$")]
    [When(@"^""([^""]+)"" adds (\d+) x ""([^""]+)"" to the cart$")]
    public Task AddToCart(string guest, int quantity, string dish) => tasks.AddToCart(guest, dish, quantity);

    [When(@"^""([^""]+)"" removes ""([^""]+)"" from the cart$")]
    public Task RemoveFromCart(string guest, string dish) => tasks.RemoveFromCart(guest, dish);

    [Given(@"^""([^""]+)"" has submitted the cart$")]
    public Task GivenSubmitted(string guest) => tasks.SubmitCart(guest);

    [When(@"^""([^""]+)"" submits an empty cart$")]
    public async Task WhenSubmitsEmpty(string guest)
    {
        var g = state.Guest(guest);
        state.LastResponse = await api.SubmitCart(g.SessionId, g.SessionToken);
    }

    [Given(@"^the Owner has pre-ordered (\d+) x ""([^""]+)""$")]
    public Task GivenOwnerPreOrdered(int quantity, string dish) => tasks.OwnerPreOrders(dish, quantity);

    [Given(@"^the Owner has submitted the group pre-order$")]
    public async Task GivenOwnerSubmitted()
    {
        var booking = state.RequireBooking();
        (await api.SubmitGroup(state.RequireOwner().Token, booking.GroupReservationId, booking.ReservationId))
            .EnsureSuccess("Submit group pre-order");
    }

    [Given(@"^another group exists with guest ""([^""]+)""$")]
    public async Task GivenAnotherGroup(string guest)
    {
        // Second Owner books the same table later the same day, so the two groups are fully independent.
        var ownBooking = state.RequireBooking();
        await Caller("another client");
        await tasks.BookForGroup(state.Clients["another client"], 3, slotOffsetHours: 3);
        await tasks.Join(guest);
        state.Booking = ownBooking;
    }

    // ---------- When: calls under test ----------

    [When(@"^(the Owner|another client|the waiter|an anonymous user|guest ""[^""]+"") requests the participants of the group$")]
    public async Task WhenRequestsParticipants(string caller) =>
        state.LastResponse = await api.GetGroupSessions(await Caller(caller), state.RequireBooking().GroupReservationId);

    [When(@"^the Owner requests the participants of a group that does not exist$")]
    public async Task WhenRequestsMissingGroup() =>
        state.LastResponse = await api.GetGroupSessions(state.RequireOwner().Token, MissingId);

    [When(@"^(the Owner|another client|the waiter|an anonymous user) submits the group pre-order$")]
    public async Task WhenSubmits(string caller)
    {
        var booking = state.RequireBooking();
        state.LastResponse = await api.SubmitGroup(await Caller(caller), booking.GroupReservationId, booking.ReservationId);
    }

    [When(@"^the Owner submits a group pre-order that does not exist$")]
    public async Task WhenSubmitsMissing() =>
        state.LastResponse = await api.SubmitGroup(state.RequireOwner().Token, MissingId, state.RequireBooking().ReservationId);

    [When(@"^(the Owner|another client|the waiter|an anonymous user) removes participant ""([^""]+)""$")]
    public async Task WhenRemoves(string caller, string guest) =>
        state.LastResponse = await api.RemoveParticipant(await Caller(caller), state.RequireBooking().GroupReservationId, state.Guest(guest).SessionId);

    [When(@"^the Owner removes participant ""([^""]+)"" from the own group$")]
    public Task WhenRemovesForeign(string guest) => WhenRemoves("the Owner", guest);

    [When(@"^the Owner removes a participant that does not exist$")]
    public async Task WhenRemovesMissing() =>
        state.LastResponse = await api.RemoveParticipant(state.RequireOwner().Token, state.RequireBooking().GroupReservationId, MissingId);

    // ---------- Then: API checks ----------

    [Then(@"^the request succeeds$")]
    public void ThenSucceeds() =>
        Assert.That(state.LastResponse!.IsSuccess, Is.True, $"Expected success, got {state.LastResponse}");

    [Then(@"^the request is rejected with status (\d+)$")]
    public void ThenRejected(int status) =>
        Assert.That(state.LastResponse!.Code, Is.EqualTo(status), state.LastResponse.ToString());

    [Then(@"^the request is rejected with status (\d+) and message ""([^""]+)""$")]
    public void ThenRejectedWithMessage(int status, string message)
    {
        ThenRejected(status);
        Assert.That(state.LastResponse!.Message, Is.EqualTo(message));
    }

    [Then(@"^the request is rejected with status (\d+) and message containing ""([^""]+)""$")]
    public void ThenRejectedWithMessageContaining(int status, string text)
    {
        ThenRejected(status);
        Assert.That(state.LastResponse!.Message, Does.Contain(text).IgnoreCase);
    }

    [Then(@"^the Owner sees (\d+) participants with total (\d+\.\d+)$")]
    public async Task ThenOwnerSeesCount(int count, decimal total)
    {
        var (participants, actualTotal) = await tasks.GroupAsSeenBy(state.RequireOwner().Token);
        Assert.Multiple(() =>
        {
            Assert.That(participants, Has.Count.EqualTo(count), string.Join(", ", participants.Select(p => p.Name)));
            Assert.That(actualTotal, Is.EqualTo(total));
        });
    }

    [Then(@"^the total equals the sum of the participants' totals$")]
    public async Task ThenTotalIsSum()
    {
        var (participants, total) = await tasks.GroupAsSeenBy(state.RequireOwner().Token);
        Assert.That(total, Is.EqualTo(participants.Sum(p => p.Total)));
    }

    [Then(@"^the Owner sees participant ""([^""]+)"" as (submitted|not submitted) with total (\d+\.\d+)$")]
    public async Task ThenOwnerSeesParticipant(string guest, string submitted, decimal total)
    {
        var (participants, _) = await tasks.GroupAsSeenBy(state.RequireOwner().Token);
        var row = participants.SingleOrDefault(p => p.Id == state.Guest(guest).SessionId);

        Assert.That(row, Is.Not.Null, $"{guest} is not in the list");
        Assert.Multiple(() =>
        {
            Assert.That(row!.Name, Is.EqualTo(state.Guest(guest).Name));
            Assert.That(row.IsSubmitted, Is.EqualTo(submitted == "submitted"));
            Assert.That(row.Total, Is.EqualTo(total));
            Assert.That(row.Items.Sum(i => i.Value * PriceOf(i.Key)), Is.EqualTo(total), "Subtotal must equal dishes × quantities");
        });
    }

    [Then(@"^the Owner does not see participant ""([^""]+)""$")]
    public async Task ThenOwnerDoesNotSee(string guest)
    {
        var (participants, _) = await tasks.GroupAsSeenBy(state.RequireOwner().Token);
        Assert.That(participants.Select(p => p.Id), Does.Not.Contain(state.Guest(guest).SessionId));
    }

    [Then(@"^the group status is ""([^""]+)""$")]
    public async Task ThenGroupStatus(string status)
    {
        var response = (await api.GetGroupStatus(state.RequireBooking().GroupReservationId)).EnsureSuccess("Get group status");
        Assert.That(Json.Str(response.Json, "status"), Is.EqualTo(status).IgnoreCase);
    }

    [Then(@"^the order has status ""([^""]+)"" with (\d+) x ""([^""]+)"" and (\d+) x ""([^""]+)""$")]
    public async Task ThenOrder(string status, int qty1, string dish1, int qty2, string dish2)
    {
        var (actualStatus, items) = await Order();
        Assert.Multiple(() =>
        {
            Assert.That(actualStatus, Is.EqualTo(status).IgnoreCase);
            Assert.That(items, Does.ContainKey(Dish(dish1)).WithValue(qty1));
            Assert.That(items, Does.ContainKey(Dish(dish2)).WithValue(qty2));
        });
    }

    [Then(@"^the order does not contain ""([^""]+)""$")]
    public async Task ThenOrderDoesNotContain(string dish)
    {
        var (_, items) = await Order();
        Assert.That(items.Keys, Does.Not.Contain(Dish(dish)));
    }

    // ---------- helpers ----------

    /// <summary>Token of the caller named in a step; null means "no Authorization header".</summary>
    private async Task<string?> Caller(string caller)
    {
        switch (caller)
        {
            case "the Owner": return state.RequireOwner().Token;
            case "the waiter": return state.RequireRestaurant().Waiter.Token;
            case "an anonymous user": return null;
            case "another client":
                if (!state.Clients.TryGetValue(caller, out var other))
                    state.Clients[caller] = other = await tasks.RegisterUser("client", "Other");
                return other.Token;
        }

        if (caller.StartsWith("guest \"")) return state.Guest(caller[7..^1]).SessionToken;
        throw new ArgumentException($"Unknown caller '{caller}'");
    }

    private string Dish(string name) => state.RequireRestaurant().DishNamed(name).Name;

    private decimal PriceOf(string dishName) =>
        state.RequireRestaurant().Menu.Single(d => d.Name == dishName).Price;

    /// <summary>Order as the waiter sees it: dish name -> quantity.</summary>
    private async Task<(string Status, Dictionary<string, int> Items)> Order()
    {
        var response = (await api.GetOrder(state.RequireRestaurant().Waiter.Token, state.RequireBooking().ReservationId))
            .EnsureSuccess("Get order");
        var json = response.Json!;
        var items = json["orderItems"]!.AsArray()
            .ToDictionary(i => i!["dish"]!["name"]!.ToString(), i => i!["quantity"]!.GetValue<int>());
        return (Json.Str(json, "status"), items);
    }
}
