using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface IAdminUserManagementService
{
    Task<IReadOnlyList<AdminUserResponse>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminUserResponse?> GetAsync(string userId, CancellationToken cancellationToken);
    Task<AdminUserOperationResult> UpdateAsync(
        string userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken);
    Task<AdminUserOperationResult> DeleteAsync(string userId, CancellationToken cancellationToken);
}
