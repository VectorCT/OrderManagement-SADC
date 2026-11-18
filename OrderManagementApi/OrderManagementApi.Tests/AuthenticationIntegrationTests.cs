using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OrderManagementApi.Tests;

public sealed class AuthenticationIntegrationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task RegisteredUserCanLogInAndUseTokenAfterRoleIsAssigned()
    {
        var client = factory.CreateClient();
        var email = $"auth-{Guid.NewGuid():N}@example.com";
        const string password = "Strong!Password2026";

        var registerResponse = await client.PostAsync(
            "/api/auth/register",
            JsonContent(new { firstName = "Test", lastName = "User", email, password }));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registration = await ReadEnvelope(registerResponse, 201);
        Assert.Equal(email, registration.GetProperty("results").GetProperty("email").GetString());
        Assert.False(registration.GetProperty("results").TryGetProperty("accessToken", out _));

        var loginResponse = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { email, password }));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await ReadEnvelope(loginResponse, 200);
        var token = login.GetProperty("results").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("Bearer", login.GetProperty("results").GetProperty("tokenType").GetString());
        Assert.Empty(login.GetProperty("results").GetProperty("roles").EnumerateArray());

        var authenticatedClient = factory.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var forbiddenResponse = await authenticatedClient.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        await ReadEnvelope(forbiddenResponse, 403);

        using (var scope = factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var createRole = await roleManager.CreateAsync(new IdentityRole("Orders.Read"));
            Assert.True(createRole.Succeeded);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            var assignRole = await userManager.AddToRoleAsync(user!, "Orders.Read");
            Assert.True(assignRole.Succeeded);
        }

        var authorizedResponse = await authenticatedClient.GetAsync("/api/customers");
        Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        await ReadEnvelope(authorizedResponse, 200);
    }

    [Fact]
    public async Task InvalidRegistrationRequestReturnsValidationEnvelope()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/auth/register",
            JsonContent(new { firstName = " ", lastName = "", email = "not-an-email", password = "weak" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await ReadEnvelope(response, 400);
        var errors = envelope.GetProperty("results");
        Assert.True(errors.TryGetProperty("FirstName", out _));
        Assert.True(errors.TryGetProperty("LastName", out _));
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task DuplicateRegistrationReturnsConflictEnvelope()
    {
        var client = factory.CreateClient();
        var email = $"duplicate-{Guid.NewGuid():N}@example.com";
        var body = new { firstName = "Test", lastName = "User", email, password = "Strong!Password2026" };

        var firstResponse = await client.PostAsync("/api/auth/register", JsonContent(body));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsync("/api/auth/register", JsonContent(body));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var envelope = await ReadEnvelope(duplicateResponse, 409);
        Assert.True(envelope.GetProperty("results").TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task InvalidCredentialsReturnGenericUnauthorizedEnvelope()
    {
        var response = await factory.CreateClient().PostAsync(
            "/api/auth/login",
            JsonContent(new { email = "missing@example.com", password = "Wrong!Password2026" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var envelope = await ReadEnvelope(response, 401);
        Assert.Equal("Email or password is incorrect.", envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RefreshCookieRotatesAndLogoutRevokesTheSession()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        const string password = "Strong!Password2026";
        var registration = await client.PostAsync(
            "/api/auth/register",
            JsonContent(new { firstName = "Test", lastName = "User", email, password }));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var login = await client.PostAsync("/api/auth/login", JsonContent(new { email, password }));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var firstCookie = GetRefreshCookie(login);
        var loginCookie = login.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("HttpOnly", loginCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", loginCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", loginCookie, StringComparison.OrdinalIgnoreCase);

        var firstAccessToken = (await ReadEnvelope(login, 200))
            .GetProperty("results").GetProperty("accessToken").GetString();
        var refresh = await PostWithCookie(client, "/api/auth/refresh", firstCookie);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var secondCookie = GetRefreshCookie(refresh);
        var secondAccessToken = (await ReadEnvelope(refresh, 200))
            .GetProperty("results").GetProperty("accessToken").GetString();
        Assert.NotEqual(firstAccessToken, secondAccessToken);

        var reusedCookie = await PostWithCookie(client, "/api/auth/refresh", firstCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedCookie.StatusCode);
        await ReadEnvelope(reusedCookie, 401);

        var logout = await PostWithCookie(client, "/api/auth/logout", secondCookie);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        await ReadEnvelope(logout, 200);

        var afterLogout = await PostWithCookie(client, "/api/auth/refresh", secondCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        await ReadEnvelope(afterLogout, 401);
    }

    private static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.StartsWith("__Host-om-refresh=", setCookie, StringComparison.Ordinal);
        return setCookie.Split(';')[0];
    }

    private static async Task<HttpResponseMessage> PostWithCookie(
        HttpClient client,
        string path,
        string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadEnvelope(HttpResponseMessage response, int statusCode)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement.Clone();
        Assert.Equal(statusCode, root.GetProperty("code").GetInt32());
        Assert.Equal(new[] { "code", "endpoint", "message", "results" },
            root.EnumerateObject().Select(property => property.Name).Order().ToArray());
        return root;
    }
}

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"auth-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Issuer", "auth-test-issuer");
        builder.UseSetting("Jwt:Audience", "auth-test-audience");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-with-at-least-32-bytes");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
