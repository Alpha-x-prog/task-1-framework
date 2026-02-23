using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pr1.MinWebService.Models;

namespace Pr1.MinWebService.Tests.Infrastructure;

public class ItemsApiTests : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ItemsApiTests(TestAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private sealed class ItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    private static string? TryGetRequestIdHeader(HttpResponseMessage resp)
    {
        foreach (var name in new[] { "X-Request-Id", "X-Request-ID", "X-RequestId" })
            if (resp.Headers.TryGetValues(name, out var vals))
                return vals.FirstOrDefault();
        return null;
    }

    private static string? FindStringByNames(JsonElement el, params string[] names)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in el.EnumerateObject())
            {
                if (names.Any(n => string.Equals(prop.Name, n, StringComparison.OrdinalIgnoreCase)))
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.ToString();

                var nested = FindStringByNames(prop.Value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var nested = FindStringByNames(item, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static (string? requestId, string? code, string? message) ExtractUnifiedError(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var requestId = FindStringByNames(root,
            "requestId", "request_id", "traceId", "correlationId");
        var code = FindStringByNames(root,
            "errorCode", "error_code", "code", "type");
        var message = FindStringByNames(root,
            "message", "error", "detail", "title");

        return (requestId, code, message);
    }

    [Fact]
    public async Task CreateItem_ThenGetById_ReturnsSameItem()
    {
        var req = new CreateItemRequest { Name = "Mouse Logitech", Price = 1999.90m };

        var createResp = await _client.PostAsJsonAsync("/api/items", req);

        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        Assert.NotNull(createResp.Headers.Location);

        var created = await createResp.Content.ReadFromJsonAsync<ItemDto>(JsonOpts);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("Mouse Logitech", created.Name);
        Assert.Equal(1999.90m, created.Price);

        var getResp = await _client.GetAsync($"/api/items/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var fetched = await getResp.Content.ReadFromJsonAsync<ItemDto>(JsonOpts);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(created.Price, fetched.Price);
    }

    [Fact]
    public async Task GetAll_ReturnsArray_AndContainsCreated()
    {
        var req = new CreateItemRequest { Name = "Keyboard", Price = 1000m };

        var create = await _client.PostAsJsonAsync("/api/items", req);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var listResp = await _client.GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<ItemDto[]>(JsonOpts);
        Assert.NotNull(list);
        Assert.Contains(list!, x => x.Name == "Keyboard");
    }

    [Fact]
    public async Task GetMissingItem_ReturnsUnifiedError_WithRequestId()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var resp = await _client.GetAsync($"/api/items/{id}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var (requestId, code, message) = ExtractUnifiedError(body);

        Assert.False(string.IsNullOrWhiteSpace(requestId));
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.False(string.IsNullOrWhiteSpace(message));

        var headerRid = TryGetRequestIdHeader(resp);
        if (!string.IsNullOrWhiteSpace(headerRid))
            Assert.Equal(headerRid, requestId);
    }

    [Fact]
    public async Task CreateInvalidItem_ReturnsUnifiedValidationError()
    {
        var req = new CreateItemRequest { Name = "   ", Price = 10m };

        var resp = await _client.PostAsJsonAsync("/api/items", req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        var (requestId, code, message) = ExtractUnifiedError(body);

        Assert.False(string.IsNullOrWhiteSpace(requestId));
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.False(string.IsNullOrWhiteSpace(message));

        var headerRid = TryGetRequestIdHeader(resp);
        if (!string.IsNullOrWhiteSpace(headerRid))
            Assert.Equal(headerRid, requestId);
    }
}
