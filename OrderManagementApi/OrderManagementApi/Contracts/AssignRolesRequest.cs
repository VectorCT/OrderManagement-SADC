namespace OrderManagementApi.Contracts;

public sealed record AssignRolesRequest(IReadOnlyList<string> Roles);
