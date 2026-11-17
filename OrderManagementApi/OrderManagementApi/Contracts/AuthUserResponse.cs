namespace OrderManagementApi.Contracts;

public sealed record AuthUserResponse(string Id, string Email, string FirstName, string LastName);
