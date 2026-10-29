using GreenTasty.E2E.Api;
using GreenTasty.E2E.Config;

namespace GreenTasty.E2E.Support;

/// <summary>
/// Business-level actions used to prepare data through the API (bottom of the test pyramid).
/// Every resource created here registers its own clean-up in <see cref="ScenarioState.Cleanup"/>.
/// </summary>
public sealed class GroupOrderTasks(RestaurantApi api, ScenarioState state)
{
    /// <summary>Test menu: names are unique per scenario, prices are fixed for exact sums.</summary>
    private static readonly (string Name, decimal Price, string Type)[] MenuTemplate =
    [
        ("Caesar Salad", 12.50m, "Appetizer"),
        ("Tomato Soup", 7.00m, "Appetizer"),
        ("Grilled Salmon", 24.00m, "MainCourse"),
        ("Cheesecake", 6.50m, "Dessert"),
    ];

    public async Task<User> RegisterUser(string role, string firstName = "Test")
    {
        var user = new User(TestData.Name(firstName), "Aqa", TestData.Email(role), Settings.TestPassword, "");
        (await api.Register(user.FirstName, user.LastName, user.Email, user.Password)).EnsureSuccess($"Register {role}");
        return user with { Token = await SignIn(user.Email, user.Password) };
    }

    public async Task<string> SignIn(string email, string password)
    {
        var response = (await api.SignIn(email, password)).EnsureSuccess($"Sign in {email}");
        return Json.Str(response.Json, "idToken");
    }

    /// <summary>Location, one table, four dishes and a waiter assigned to the location.</summary>
    public async Task<Restaurant> SetUpRestaurant(int tableCapacity = 6)
    {
        var address = TestData.Address();
        var location = (await api.CreateLocation(address)).EnsureSuccess("Create location");
        var locationId = Json.Str(location.Json, "id");

        // A waiter is mandatory: a reservation is always assigned to the least busy waiter of the location.
        var waiter = await RegisterUser("waiter", "Waiter");
        (await api.AddWaiter(waiter.Email, locationId)).EnsureSuccess("Add waiter");
        waiter = waiter with { Token = await SignIn(waiter.Email, waiter.Password) }; // new token carries the Waiter group
        state.Cleanup.Push(("remove waiter", () => api.RemoveWaiter(waiter.Email, locationId)));
        state.Cleanup.Push(("delete location", () => api.DeleteLocation(locationId, waiter.Token)));

        var table = (await api.CreateTable(locationId, tableCapacity, Random.Shared.Next(100, 999))).EnsureSuccess("Create table");
        var tableId = Json.Str(table.Json, "id");
        state.Cleanup.Push(("delete table", () => api.DeleteTable(tableId, waiter.Token)));

        var suffix = TestData.Unique()[..4];
        var menu = new List<Dish>();
        foreach (var (name, price, type) in MenuTemplate)
        {
            var dishName = $"{name} {suffix}";
            var created = (await api.CreateDish(dishName, price, type, locationId)).EnsureSuccess($"Create dish {name}");
            var dishId = Json.Str(created.Json, "id");
            menu.Add(new Dish(dishId, dishName, price));
            state.Cleanup.Push(($"delete dish {name}", () => api.DeleteDish(dishId, waiter.Token)));
        }

        return state.Restaurant = new Restaurant(locationId, address, tableId, tableCapacity, menu, waiter);
    }

    /// <summary>Owner books the table for a group; the backend creates the group pre-order automatically.</summary>
    public async Task<GroupBooking> BookForGroup(User owner, int guestCount, int slotOffsetHours = 0)
    {
        var restaurant = state.RequireRestaurant();
        var (start, end) = TestData.Slot();
        (start, end) = (start.AddHours(slotOffsetHours), end.AddHours(slotOffsetHours));

        var reservation = (await api.CreateReservation(owner.Token, restaurant.TableId, start, end, guestCount))
            .EnsureSuccess("Create reservation");
        var reservationId = Json.Str(reservation.Json, "id");
        state.Cleanup.Push(("cancel reservation", () => api.CancelReservation(owner.Token, reservationId)));

        var invite = (await api.GetInviteToken(owner.Token, reservationId)).EnsureSuccess("Get invite token");
        var inviteToken = Json.Str(invite.Json, "token");

        var group = (await api.GetInvite(inviteToken)).EnsureSuccess("Open invitation");
        var groupId = Json.Str(group.Json, "groupReservationId");

        return state.Booking = new GroupBooking(reservationId, groupId, inviteToken);
    }

    public async Task<Guest> Join(string guestName)
    {
        var booking = state.RequireBooking();
        var uniqueName = TestData.Name(guestName);

        var invite = (await api.GetInvite(booking.InviteToken)).EnsureSuccess("Open invitation");
        var groupId = Json.Str(invite.Json, "groupReservationId");

        var session = (await api.JoinGroup(groupId, uniqueName)).EnsureSuccess($"{guestName} joins");
        var guest = new Guest(uniqueName, Json.Str(session.Json, "sessionId"), Json.Str(session.Json, "sessionToken"));
        state.Guests[guestName] = guest;
        return guest;
    }

    public async Task AddToCart(string guestName, string dishName, int quantity)
    {
        var guest = state.Guest(guestName);
        var dish = state.RequireRestaurant().DishNamed(dishName);
        (await api.AddDishToCart(guest.SessionId, guest.SessionToken, dish.Id, quantity)).EnsureSuccess($"{guestName} adds {dishName}");
    }

    public async Task RemoveFromCart(string guestName, string dishName)
    {
        var guest = state.Guest(guestName);
        var dish = state.RequireRestaurant().DishNamed(dishName);
        (await api.RemoveDishFromCart(guest.SessionId, guest.SessionToken, dish.Id)).EnsureSuccess($"{guestName} removes {dishName}");
    }

    public async Task SubmitCart(string guestName)
    {
        var guest = state.Guest(guestName);
        (await api.SubmitCart(guest.SessionId, guest.SessionToken)).EnsureSuccess($"{guestName} submits cart");
    }

    /// <summary>
    /// The backend merges the group into the Owner's existing order, so the Owner places a personal
    /// pre-order first (see README, "Behaviour chosen for undefined cases").
    /// </summary>
    public async Task OwnerPreOrders(string dishName, int quantity)
    {
        var dish = state.RequireRestaurant().DishNamed(dishName);
        var booking = state.RequireBooking();
        (await api.CreateOrder(state.RequireOwner().Token, booking.ReservationId, (dish.Id, quantity))).EnsureSuccess("Owner pre-order");
    }

    public async Task<(IReadOnlyList<ParticipantView> Participants, decimal Total)> GroupAsSeenBy(string token)
    {
        var response = (await api.GetGroupSessions(token, state.RequireBooking().GroupReservationId)).EnsureSuccess("Get group sessions");
        var participants = response.Json!["participants"]!.AsArray().Select(p => ParticipantView.From(p!)).ToList();
        return (participants, response.Json!["total"]!.GetValue<decimal>());
    }
}
