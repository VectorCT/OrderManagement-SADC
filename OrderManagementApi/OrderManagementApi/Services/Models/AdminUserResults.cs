using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Models;

public sealed record AdminUserOperationResult(
    bool UserFound,
    bool Succeeded,
    bool Conflict,
    IReadOnlyDictionary<string, string[]> Errors,
    AdminUserResponse? User);
