using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using DataAccessLayer.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;

namespace OrderManagementApi.Tests;

public sealed class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UnauthenticatedRequest_ReturnsCustom401Envelope()
    {
        var response = await factory.CreateClient().GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertEnvelope(response, 401, "/api/customers");
    }

    [Fact]
    public async Task MessagingDiagnostics_RequiresAdminPermission()
    {
        var client = factory.CreateClient();
        var unauthenticated = await client.GetAsync("/api/admin/messaging");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        await AssertEnvelope(unauthenticated, 401, "/api/admin/messaging");

        SetPermission(client, "Orders.Read");
        var forbidden = await client.GetAsync("/api/admin/messaging");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await AssertEnvelope(forbidden, 403, "/api/admin/messaging");
    }

    [Fact]
    public async Task ReadPolicy_AllowsReadRoleAndReturnsPagedEnvelope()
    {
        factory.CustomerService.Reset();
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Read");

        var response = await client.GetAsync("/api/customers?search=smith&page=2&pageSize=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await AssertEnvelope(response, 200, "/api/customers");
        Assert.Equal(2, json.RootElement.GetProperty("results").GetProperty("page").GetInt32());
        Assert.Equal(15, json.RootElement.GetProperty("results").GetProperty("pageSize").GetInt32());
        Assert.Equal(2, factory.CustomerService.LastPage);
        Assert.Equal(15, factory.CustomerService.LastPageSize);
        Assert.Equal("smith", factory.CustomerService.LastSearch);
    }

    [Fact]
    public async Task WriteRoleCannotUseAdminOnlyRoute_Returns403Envelope()
    {
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Write");

        var response = await client.PutAsync(
            $"/api/orders/{Guid.NewGuid()}/status",
            JsonContent("{\"status\":\"Paid\"}"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertEnvelope(response, 403, $"/api/orders/{Guid.NewGuid()}/status", checkEndpoint: false);
    }

    [Fact]
    public async Task InvalidCustomerRequest_FailsValidationBeforeCallingService()
    {
        factory.CustomerService.Reset();
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Write");

        var response = await client.PostAsync(
            "/api/customers",
            JsonContent("{\"name\":\" \",\"email\":\"bad\",\"countryCode\":\"US\"}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await AssertEnvelope(response, 400, "/api/customers");
        Assert.Equal(0, factory.CustomerService.CreateCalls);
        var errors = json.RootElement.GetProperty("results");
        Assert.True(errors.TryGetProperty("Name", out _));
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("CountryCode", out _));
    }

    [Fact]
    public async Task ValidCustomerPost_Returns201LocationAndEnvelope()
    {
        factory.CustomerService.Reset();
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Write");

        var response = await client.PostAsync(
            "/api/customers",
            JsonContent("{\"name\":\"Example\",\"email\":\"example@example.com\",\"countryCode\":\"ZA\"}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(factory.CustomerService.Customer.Id.ToString(), response.Headers.Location!.ToString());
        await AssertEnvelope(response, 201, "/api/customers");
        Assert.Equal(1, factory.CustomerService.CreateCalls);
    }

    [Fact]
    public async Task InvalidOrderRequest_FailsValidationBeforeCallingService()
    {
        factory.OrderService.Reset();
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Write");

        var response = await client.PostAsync(
            "/api/orders",
            JsonContent("{\"customerId\":\"00000000-0000-0000-0000-000000000000\",\"currencyCode\":\"USD\",\"lineItems\":[]}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await AssertEnvelope(response, 400, "/api/orders");
        Assert.Equal(0, factory.OrderService.CreateCalls);
        Assert.NotEqual(JsonValueKind.Null, json.RootElement.GetProperty("results").ValueKind);
    }

    [Fact]
    public async Task MissingCustomerReturns404Envelope()
    {
        factory.CustomerService.Reset();
        factory.CustomerService.ReturnNotFound = true;
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Read");

        var id = Guid.NewGuid();
        var response = await client.GetAsync($"/api/customers/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertEnvelope(response, 404, $"/api/customers/{id}");
        factory.CustomerService.ReturnNotFound = false;
    }

    [Fact]
    public async Task StatusTransitionRequiresIdempotencyKeyAndReturnsConflictEnvelope()
    {
        factory.OrderService.Reset();
        factory.OrderService.TransitionResult = Result.Failure<OrderDto>("Order status cannot transition from Pending to Fulfilled.");
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Admin");
        var path = $"/api/orders/{Guid.NewGuid()}/status";

        var missingKey = await client.PutAsync(path, JsonContent("{\"status\":\"Paid\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        await AssertEnvelope(missingKey, 400, path);
        Assert.Equal(0, factory.OrderService.TransitionCalls);

        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent("{\"status\":\"Fulfilled\"}") };
        request.Headers.Add("Idempotency-Key", "test-transition-1");
        var conflict = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await AssertEnvelope(conflict, 409, path);
        Assert.Equal(1, factory.OrderService.TransitionCalls);
    }

    [Fact]
    public async Task InvalidStatusModel_ReturnsCustom400WithoutCallingService()
    {
        factory.OrderService.Reset();
        var client = factory.CreateClient();
        SetPermission(client, "Orders.Admin");
        var path = $"/api/orders/{Guid.NewGuid()}/status";
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent("{\"status\":\"NotAStatus\"}") };
        request.Headers.Add("Idempotency-Key", "status-invalid");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertEnvelope(response, 400, path);
        Assert.Equal(0, factory.OrderService.TransitionCalls);
    }

    private static void SetPermission(HttpClient client, string permission) =>
        client.DefaultRequestHeaders.Add("X-Test-Permission", permission);

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> AssertEnvelope(
        HttpResponseMessage response,
        int code,
        string endpoint,
        bool checkEndpoint = true)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        var json = await JsonDocument.ParseAsync(stream);
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
        Assert.Equal(new[] { "code", "endpoint", "message", "results" }, properties);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("message").GetString()));
        if (checkEndpoint)
        {
            Assert.Equal(endpoint, json.RootElement.GetProperty("endpoint").GetString());
        }
        return json;
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public TestCustomerService CustomerService { get; } = new();
    public TestOrderService OrderService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "test-issuer");
        builder.UseSetting("Jwt:Audience", "test-audience");
        builder.UseSetting("Jwt:SigningKey", "test-only-key-that-is-never-used-to-issue-tokens");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICustomerService>();
            services.RemoveAll<IOrderService>();
            services.AddSingleton<ICustomerService>(CustomerService);
            services.AddSingleton<IOrderService>(OrderService);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Permission", out var permissions))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = permissions.SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(permission => new Claim(ClaimTypes.Role, permission));
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(ApiResponse.Create(401, "Authentication is required.", null, Context), Context.RequestAborted);
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(ApiResponse.Create(403, "You are not authorized to perform this operation.", null, Context), Context.RequestAborted);
    }
}

public sealed class TestCustomerService : ICustomerService
{
    public CustomerDto Customer { get; } = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Example", "example@example.com", "ZA", DateTime.UtcNow);
    public int CreateCalls { get; private set; }
    public int LastPage { get; private set; }
    public int LastPageSize { get; private set; }
    public string? LastSearch { get; private set; }
    public bool ReturnNotFound { get; set; }

    public Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        CreateCalls++;
        return Task.FromResult(Result.Success(Customer));
    }

    public Task<Result<CustomerDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(ReturnNotFound
            ? Result.Failure<CustomerDto>("Customer was not found.")
            : Result.Success(Customer));

    public Task<Result<IReadOnlyList<CustomerDto>>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        LastSearch = search;
        LastPage = page;
        LastPageSize = pageSize;
        return Task.FromResult(Result.Success<IReadOnlyList<CustomerDto>>([Customer]));
    }

    public void Reset()
    {
        CreateCalls = 0;
        LastPage = 0;
        LastPageSize = 0;
        LastSearch = null;
        ReturnNotFound = false;
    }
}

public sealed class TestOrderService : IOrderService
{
    private readonly OrderDto _order = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "Pending",
        DateTime.UtcNow,
        "ZAR",
        25.00m,
        [],
        [],
        null,
        null);

    public int CreateCalls { get; private set; }
    public int TransitionCalls { get; private set; }
    public Result<OrderDto> TransitionResult { get; set; } = Result.Success(new OrderDto(
        Guid.NewGuid(), Guid.NewGuid(), "Paid", DateTime.UtcNow, "ZAR", 25m, [], [], null, null));

    public Task<Result<OrderDto>> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        CreateCalls++;
        return Task.FromResult(Result.Success(_order));
    }

    public Task<Result<OrderDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Result.Success(_order));

    public Task<Result<IReadOnlyList<OrderSummaryDto>>> ListAsync(
        Guid? customerId,
        OrderStatus? status,
        int page,
        int pageSize,
        string? sort,
        CancellationToken ct = default) =>
        Task.FromResult(Result.Success<IReadOnlyList<OrderSummaryDto>>([]));

    public Task<Result<OrderDto>> TransitionStatusAsync(Guid id, OrderStatus targetStatus, string idempotencyKey, CancellationToken ct = default)
    {
        TransitionCalls++;
        return Task.FromResult(TransitionResult);
    }

    public void Reset()
    {
        CreateCalls = 0;
        TransitionCalls = 0;
        TransitionResult = Result.Success(_order);
    }
}