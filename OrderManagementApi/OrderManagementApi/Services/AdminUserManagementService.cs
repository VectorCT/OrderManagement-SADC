using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services;

public sealed class AdminUserManagementService(UserManager<ApplicationUser> userManager) : IAdminUserManagementService
{
    public async Task<IReadOnlyList<AdminUserResponse>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.NormalizedEmail)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var results = new List<AdminUserResponse>(users.Count);
        foreach (var user in users)
        {
            results.Add(await ToResponseAsync(user));
        }

        return results;
    }

    public async Task<AdminUserResponse?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await ToResponseAsync(user);
    }

    public async Task<AdminUserOperationResult> UpdateAsync(
        string userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Failure(userFound: false);
        }

        var email = request.Email.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id)
        {
            return new AdminUserOperationResult(
                true,
                false,
                true,
                new Dictionary<string, string[]> { [nameof(request.Email)] = ["An account with this email already exists."] },
                null);
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;
        user.UserName = email;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return new AdminUserOperationResult(
                true,
                false,
                result.Errors.Any(error => error.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)),
                result.Errors.ToDictionary(error => error.Code, error => new[] { error.Description }),
                null);
        }

        return new AdminUserOperationResult(true, true, false, new Dictionary<string, string[]>(), await ToResponseAsync(user));
    }

    public async Task<AdminUserOperationResult> DeleteAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Failure(userFound: false);
        }

        var adminUsers = (await userManager.GetUsersInRoleAsync("Admin"))
            .Concat(await userManager.GetUsersInRoleAsync("Orders.Admin"))
            .DistinctBy(candidate => candidate.Id)
            .ToArray();
        if (adminUsers.Length <= 1 && adminUsers.Any(candidate => candidate.Id == user.Id))
        {
            return new AdminUserOperationResult(
                true,
                false,
                true,
                new Dictionary<string, string[]> { ["User"] = ["The last administrator cannot be deleted."] },
                null);
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return new AdminUserOperationResult(
                true,
                false,
                false,
                result.Errors.ToDictionary(error => error.Code, error => new[] { error.Description }),
                null);
        }

        return new AdminUserOperationResult(true, true, false, new Dictionary<string, string[]>(), null);
    }

    private async Task<AdminUserResponse> ToResponseAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.CreatedAt,
            roles.Order(StringComparer.Ordinal).ToArray());
    }

    private static AdminUserOperationResult Failure(bool userFound) =>
        new(userFound, false, false, new Dictionary<string, string[]>(), null);
}
