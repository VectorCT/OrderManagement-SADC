namespace OrderManagementApi.Contracts;

public sealed record AdminUserResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record UpdateAdminUserRequest(string FirstName, string LastName, string Email);
