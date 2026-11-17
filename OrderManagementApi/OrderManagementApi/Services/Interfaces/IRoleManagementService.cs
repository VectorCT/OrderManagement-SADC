using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface IRoleManagementService
{
    Task<IReadOnlyList<string>> GetAvailableRolesAsync(CancellationToken cancellationToken);
    Task<UserRolesResult> GetRolesAsync(string userId, CancellationToken cancellationToken);
    Task<RoleAssignmentResult> AssignRolesAsync(
        string userId,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken);
}
