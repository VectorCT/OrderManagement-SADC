namespace OrderManagementApi.Services.Models;

public sealed record CreateCustomerRequest(string Name, string Email, string CountryCode);

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string CountryCode,
    DateTime CreatedAt);
