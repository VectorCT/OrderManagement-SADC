using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DataAccessLayer.Contexts;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderManagementApi.Services;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Tests;

public sealed class RoleManagementIntegrationTests
{
    [Fact]
    public async Task SeederCreatesSadcOrdersAndRolesIdempotentlyAndRoleEndpointsAreAdminOnly()
    {
        using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        const string adminEmail = "ctshabalala89@gmail.com";
        const string password = "Strong!Password2026";
        string adminUserId;
        string memberUserId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Calvin",
                LastName = "Tshabalala"
            };
            Assert.True((await userManager.CreateAsync(admin, password)).Succeeded);
            adminUserId = admin.Id;

            var member = new ApplicationUser
            {
                UserName = "member@example.com",
                Email = "member@example.com",
                FirstName = "New",
                LastName = "Member"
            };
            Assert.True((await userManager.CreateAsync(member, password)).Succeeded);
            memberUserId = member.Id;

            var seeder = scope.ServiceProvider.GetRequiredService<ApplicationDataSeeder>();
            await seeder.SeedAsync(adminEmail, seedOrders: true, CancellationToken.None);
            await seeder.SeedAsync(adminEmail, seedOrders: true, CancellationToken.None);

            Assert.Equal(1024, await dbContext.Orders.CountAsync());
            Assert.Equal(16, await dbContext.Customers.CountAsync());
            Assert.True(await userManager.IsInRoleAsync(admin, "Admin"));

            var orders = await dbContext.Orders
                .Include(order => order.Customer)
                .Include(order => order.LineItems)
                .ToListAsync();
            var currencyPolicy = scope.ServiceProvider.GetRequiredService<ISadcCurrencyPolicy>();
            Assert.All(orders, order =>
            {
                Assert.NotNull(order.Customer);
                Assert.NotEmpty(order.LineItems);
                Assert.True(currencyPolicy.IsCurrencyAllowed(order.Customer!.CountryCode, order.CurrencyCode));
                Assert.Equal(order.LineItems.Sum(item => item.Quantity * item.UnitPrice), order.TotalAmount);
            });
            Assert.Contains(orders, order => order.Customer!.CountryCode == "ZW" && order.CurrencyCode == "USD");
            Assert.Contains(orders, order => order.Customer!.CountryCode == "ZW" && order.CurrencyCode == "ZWL");
            Assert.Equal(16, orders.Select(order => order.Customer!.CountryCode).Distinct().Count());
        }

        var adminToken = await Login(client, adminEmail, password);
        var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var rolesResponse = await adminClient.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var roles = await ReadEnvelope(rolesResponse, 200);
        Assert.Equal(
            new[] { "Admin", "Orders.Admin", "Orders.Read", "Orders.Write" },
            roles.GetProperty("results").EnumerateArray().Select(role => role.GetString()).ToArray());

        var userRolesResponse = await adminClient.GetAsync($"/api/roles/users/{memberUserId}");
        Assert.Equal(HttpStatusCode.OK, userRolesResponse.StatusCode);
        Assert.Empty((await ReadEnvelope(userRolesResponse, 200)).GetProperty("results").EnumerateArray());

        var usersResponse = await adminClient.GetAsync("/api/admin/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var listedUsers = (await ReadEnvelope(usersResponse, 200)).GetProperty("results").GetProperty("items");
        var listedMember = listedUsers.EnumerateArray().Single(user => user.GetProperty("id").GetString() == memberUserId);
        Assert.Empty(listedMember.GetProperty("roles").EnumerateArray());

        var createUserResponse = await adminClient.PostAsync(
            "/api/admin/users",
            JsonContent(new { firstName = "Created", lastName = "By Admin", email = "admin-created@example.com", password }));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        var createdUser = (await ReadEnvelope(createUserResponse, 201)).GetProperty("results");
        var createdUserId = createdUser.GetProperty("id").GetString()!;
        Assert.Empty(createdUser.GetProperty("roles").EnumerateArray());

        var updateUserResponse = await adminClient.PutAsync(
            $"/api/admin/users/{memberUserId}",
            JsonContent(new { firstName = "Updated", lastName = "Member", email = "updated-member@example.com" }));
        Assert.Equal(HttpStatusCode.OK, updateUserResponse.StatusCode);
        Assert.Equal(
            "updated-member@example.com",
            (await ReadEnvelope(updateUserResponse, 200)).GetProperty("results").GetProperty("email").GetString());

        var assignResponse = await adminClient.PostAsync(
            $"/api/roles/users/{memberUserId}",
            JsonContent(new { roles = new[] { "Orders.Read", "Orders.Write" } }));
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);
        var assigned = await ReadEnvelope(assignResponse, 200);
        Assert.Equal(
            new[] { "Orders.Read", "Orders.Write" },
            assigned.GetProperty("results").EnumerateArray().Select(role => role.GetString()).ToArray());

        var updatedRolesResponse = await adminClient.GetAsync($"/api/roles/users/{memberUserId}");
        Assert.Equal(HttpStatusCode.OK, updatedRolesResponse.StatusCode);
        var updatedRoles = await ReadEnvelope(updatedRolesResponse, 200);
        Assert.Equal(
            new[] { "Orders.Read", "Orders.Write" },
            updatedRoles.GetProperty("results").EnumerateArray().Select(role => role.GetString()).ToArray());

        var deleteUserResponse = await adminClient.DeleteAsync($"/api/admin/users/{createdUserId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteUserResponse.StatusCode);
        var deleteAdminResponse = await adminClient.DeleteAsync($"/api/admin/users/{adminUserId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteAdminResponse.StatusCode);
        await ReadEnvelope(deleteAdminResponse, 409);

        var invalidRoleResponse = await adminClient.PostAsync(
            $"/api/roles/users/{memberUserId}",
            JsonContent(new { roles = new[] { "SuperUser" } }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRoleResponse.StatusCode);
        await ReadEnvelope(invalidRoleResponse, 400);

        var memberToken = await Login(client, "updated-member@example.com", password);
        var memberClient = factory.CreateClient();
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var forbiddenResponse = await memberClient.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        await ReadEnvelope(forbiddenResponse, 403);
        var forbiddenUsersResponse = await memberClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUsersResponse.StatusCode);
        await ReadEnvelope(forbiddenUsersResponse, 403);
    }

    private static async Task<string> Login(HttpClient client, string email, string password)
    {
        var response = await client.PostAsync("/api/auth/login", JsonContent(new { email, password }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadEnvelope(response, 200))
            .GetProperty("results")
            .GetProperty("accessToken")
            .GetString()!;
    }

    private static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadEnvelope(HttpResponseMessage response, int statusCode)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = document.RootElement.Clone();
        Assert.Equal(statusCode, root.GetProperty("code").GetInt32());
        Assert.Equal(
            new[] { "code", "endpoint", "message", "results" },
            root.EnumerateObject().Select(property => property.Name).Order().ToArray());
        return root;
    }
}
