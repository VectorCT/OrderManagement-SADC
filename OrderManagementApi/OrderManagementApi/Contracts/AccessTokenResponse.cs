namespace OrderManagementApi.Contracts;

public sealed record AccessTokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    AuthUserResponse User,
    IReadOnlyList<string> Roles);
