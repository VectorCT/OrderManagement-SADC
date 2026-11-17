using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Models;

public sealed record RegistrationResult(
    bool Succeeded,
    bool Conflict,
    AuthUserResponse? User,
    IReadOnlyDictionary<string, string[]> Errors);

public sealed record LoginResult(AccessTokenResponse? Token);
