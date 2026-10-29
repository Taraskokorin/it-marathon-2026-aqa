using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenTasty.E2E.Config;

namespace GreenTasty.E2E.Api;

/// <summary>Raw HTTP response: status plus the body parsed as JSON when possible.</summary>
public sealed record ApiResponse(HttpStatusCode Status, string Body)
{
    public int Code => (int)Status;

    public bool IsSuccess => Code is >= 200 and < 300;

    public JsonNode? Json
    {
        get
        {
            try { return JsonNode.Parse(Body); }
            catch (JsonException) { return null; }
        }
    }

    /// <summary>Error text from {"message": "..."} or a plain JSON string body.</summary>
    public string Message =>
        Json switch
        {
            JsonObject o when o["message"] is not null => o["message"]!.ToString(),
            JsonValue v => v.ToString(),
            _ => Body
        };

    public ApiResponse EnsureSuccess(string action)
    {
        if (!IsSuccess)
            throw new InvalidOperationException($"{action} failed: {Code} {Body}");
        return this;
    }

    public override string ToString() => $"{Code} {Body}";
}

/// <summary>Thin HttpClient wrapper over the deployed API Gateway stage.</summary>
public sealed class ApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public Task<ApiResponse> Get(string path, string? token = null) =>
        Send(HttpMethod.Get, path, null, token);

    public Task<ApiResponse> Post(string path, object? body, string? token = null) =>
        Send(HttpMethod.Post, path, body, token);

    public Task<ApiResponse> Put(string path, object? body, string? token = null) =>
        Send(HttpMethod.Put, path, body, token);

    public Task<ApiResponse> Delete(string path, object? body = null, string? token = null) =>
        Send(HttpMethod.Delete, path, body, token);

    private static async Task<ApiResponse> Send(HttpMethod method, string path, object? body, string? token)
    {
        using var request = new HttpRequestMessage(method, Settings.ApiUrl + path);

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        // Lambda cold starts on a fresh deployment occasionally return 502/503: retry once.
        for (var attempt = 1; ; attempt++)
        {
            using var clone = attempt == 1 ? null : await CloneAsync(request);
            using var response = await Http.SendAsync(clone ?? request);
            var text = await response.Content.ReadAsStringAsync();

            if (attempt < 2 && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(1500);
                continue;
            }

            TestContext.Progress.WriteLine($"  {method} {path} -> {(int)response.StatusCode}");
            return new ApiResponse(response.StatusCode, text);
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var copy = new HttpRequestMessage(original.Method, original.RequestUri);
        copy.Headers.Authorization = original.Headers.Authorization;
        if (original.Content is not null)
            copy.Content = new StringContent(await original.Content.ReadAsStringAsync(), Encoding.UTF8, "application/json");
        return copy;
    }
}
