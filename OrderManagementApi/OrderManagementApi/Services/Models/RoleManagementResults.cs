namespace OrderManagementApi.Services.Models;

public sealed record UserRolesResult(bool UserFound, IReadOnlyList<string> Roles);

public sealed record RoleAssignmentResult(
    bool UserFound,
    bool Succeeded,
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyList<string> Roles);
