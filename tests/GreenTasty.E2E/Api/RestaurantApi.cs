namespace GreenTasty.E2E.Api;

/// <summary>Endpoints of the Green &amp; Tasty backend used by the tests (paths as in RouteRegistry.cs).</summary>
public sealed class RestaurantApi(ApiClient client)
{
    // Auth
    public Task<ApiResponse> Register(string firstName, string lastName, string email, string password) =>
        client.Post("/register", new { firstName, lastName, email, password, confirmPassword = password });

    public Task<ApiResponse> SignIn(string email, string password) =>
        client.Post("/signin", new { email, password });

    // Restaurant set-up (test data)
    public Task<ApiResponse> CreateLocation(string address) =>
        client.Post("/locations", new
        {
            address,
            description = "Location created by automated tests",
            totalCapacity = 20,
            averageOccupancy = "50%",
            imageBase64 = (string?)null,
            rating = 4.5
        });

    public Task<ApiResponse> DeleteLocation(string id, string token) => client.Delete($"/locations/{id}", token: token);

    public Task<ApiResponse> CreateTable(string locationId, int capacity, int tableNumber) =>
        client.Post("/tables", new { locationId, capacity, tableNumber });

    public Task<ApiResponse> DeleteTable(string id, string token) => client.Delete($"/tables/{id}", token: token);

    public Task<ApiResponse> CreateDish(string name, decimal price, string dishType, string locationId) =>
        client.Post("/dishes", new
        {
            name,
            description = $"{name} (test dish)",
            dishType,
            price,
            weight = "300 g",
            calories = "250 kcal",
            proteins = "10 g",
            fats = "8 g",
            carbohydrates = "30 g",
            vitamins = "C",
            imageBase64 = (string?)null,
            state = "Available",
            locationIds = new[] { locationId }
        });

    public Task<ApiResponse> DeleteDish(string id, string token) => client.Delete($"/dishes/{id}", token: token);

    public Task<ApiResponse> AddWaiter(string email, string locationId) =>
        client.Post("/waiters", new { email, locationId });

    public Task<ApiResponse> RemoveWaiter(string email, string locationId) =>
        client.Delete("/waiters", new { email, locationId });

    // Reservations and pre-orders
    public Task<ApiResponse> CreateReservation(string token, string tableId, DateTime startUtc, DateTime endUtc, int guestCount) =>
        client.Post("/reservations", new
        {
            tableId,
            customerEmail = (string?)null,
            startTime = startUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            endTime = endUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            guestCount,
            customerId = (string?)null
        }, token);

    public Task<ApiResponse> CancelReservation(string token, string reservationId) =>
        client.Delete("/reservations", new { id = reservationId }, token);

    public Task<ApiResponse> GetInviteToken(string token, string reservationId) =>
        client.Get($"/reservations/{reservationId}/invite-token", token);

    public Task<ApiResponse> CreateOrder(string token, string reservationId, params (string DishId, int Quantity)[] items) =>
        client.Post("/orders", new
        {
            reservationId,
            orderItems = items.Select(i => new { dishId = i.DishId, quantity = i.Quantity }).ToArray()
        }, token);

    public Task<ApiResponse> GetOrder(string token, string reservationId) => client.Get($"/orders/{reservationId}", token);

    // Guests (anonymous, authorised by the guest session token)
    public Task<ApiResponse> GetInvite(string inviteToken) => client.Get($"/guest/invites/{inviteToken}");

    public Task<ApiResponse> JoinGroup(string groupReservationId, string guestName) =>
        client.Post("/guest/sessions", new { groupReservationId, guestName });

    public Task<ApiResponse> AddDishToCart(string sessionId, string sessionToken, string dishId, int quantity) =>
        client.Post($"/guest/sessions/{sessionId}/dishes", new { dishId, quantity }, sessionToken);

    public Task<ApiResponse> RemoveDishFromCart(string sessionId, string sessionToken, string dishId) =>
        client.Delete($"/guest/sessions/{sessionId}/dishes", new { dishId }, sessionToken);

    public Task<ApiResponse> SubmitCart(string sessionId, string sessionToken) =>
        client.Post($"/guest/sessions/{sessionId}/submit", null, sessionToken);

    // Group pre-order for the Owner (homework #5)
    public Task<ApiResponse> GetGroupSessions(string? token, string groupReservationId) =>
        client.Get($"/guest/groups/{groupReservationId}/sessions", token);

    public Task<ApiResponse> GetGroupStatus(string groupReservationId) =>
        client.Get($"/guest/groups/{groupReservationId}/status");

    public Task<ApiResponse> SubmitGroup(string? token, string groupReservationId, string reservationId) =>
        client.Post($"/group-reservations/{groupReservationId}/submit", new { reservationId }, token);

    public Task<ApiResponse> RemoveParticipant(string? token, string groupReservationId, string guestSessionId) =>
        client.Delete($"/guest/groups/{groupReservationId}/sessions/{guestSessionId}", token: token);
}
