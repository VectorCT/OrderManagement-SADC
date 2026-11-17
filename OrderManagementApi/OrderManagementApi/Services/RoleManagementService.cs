using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services;

public sealed class RoleManagementService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : IRoleManagementService
{
    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await roleManager.Roles
            .AsNoTracking()
            .Where(role => role.Name != null)
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRolesResult> GetRolesAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new UserRolesResult(false, Array.Empty<string>());
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserRolesResult(true, roles.Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<RoleAssignmentResult> AssignRolesAsync(
        string userId,
        IReadOnlyList<string> requestedRoles,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new RoleAssignmentResult(false, false, new Dictionary<string, string[]>(), Array.Empty<string>());
        }

        var roles = requestedRoles
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var errors = new Dictionary<string, string[]>();
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                errors[role] = ["The requested role does not exist."];
            }
        }

        if (errors.Count > 0)
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            return new RoleAssignmentResult(true, false, errors, currentRoles.Order(StringComparer.Ordinal).ToArray());
        }

        var current = await userManager.GetRolesAsync(user);
        var toAdd = roles.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        if (toAdd.Length > 0)
        {
            var result = await userManager.AddToRolesAsync(user, toAdd);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    errors[error.Code] = [error.Description];
                }

                current = await userManager.GetRolesAsync(user);
                return new RoleAssignmentResult(true, false, errors, current.Order(StringComparer.Ordinal).ToArray());
            }
        }

        current = await userManager.GetRolesAsync(user);
        return new RoleAssignmentResult(true, true, errors, current.Order(StringComparer.Ordinal).ToArray());
    }
}
