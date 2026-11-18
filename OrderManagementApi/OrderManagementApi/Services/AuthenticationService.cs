using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptions<JwtAuthenticationOptions> jwtOptions) : IAuthenticationService
{
    private const string RefreshTokenProvider = "OrderManagement";
    private const string RefreshTokenHashPrefix = "RefreshTokenHash:";
    private const string RefreshTokenExpiryPrefix = "RefreshTokenExpiry:";
    private readonly JwtAuthenticationOptions _jwtOptions = jwtOptions.Value;

    public async Task<RegistrationResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (result.Succeeded)
        {
            return new RegistrationResult(true, false, ToResponse(user), new Dictionary<string, string[]>());
        }

        var errors = result.Errors
            .GroupBy(error => error.Code is "DuplicateEmail" or "DuplicateUserName"
                ? nameof(RegisterRequest.Email)
                : error.Code.StartsWith("Password", StringComparison.Ordinal)
                    ? nameof(RegisterRequest.Password)
                    : "Request")
            .ToDictionary(
                group => group.Key,
                group => group.Select(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"
                        ? "An account with this email already exists."
                        : error.Description).Distinct().ToArray());
        var conflict = result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName");
        return new RegistrationResult(false, conflict, null, errors);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return new LoginResult(null, null);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        return signInResult.Succeeded
            ? await CreateSessionAsync(user)
            : new LoginResult(null, null);
    }

    public async Task<LoginResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await ResolveRefreshTokenAsync(refreshToken, checkExpiry: true);
        return session is null
            ? new LoginResult(null, null)
            : await CreateSessionAsync(session.Value.User, session.Value.SessionId);
    }

    public async Task RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parsedToken = ParseRefreshToken(refreshToken);
        if (parsedToken is null)
        {
            return;
        }

        var (userId, sessionId, secret) = parsedToken.Value;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        var storedHash = await userManager.GetAuthenticationTokenAsync(
            user,
            RefreshTokenProvider,
            RefreshTokenHashName(sessionId));
        if (MatchesRefreshToken(secret, storedHash))
        {
            await RemoveRefreshTokenAsync(user, sessionId);
        }
    }

    private async Task<LoginResult> CreateSessionAsync(ApplicationUser user, string? sessionId = null)
    {
        var accessToken = await CreateAccessTokenAsync(user);
        var refreshToken = await CreateRefreshTokenAsync(user, sessionId);
        return new LoginResult(accessToken, refreshToken);
    }

    private async Task<AccessTokenResponse> CreateAccessTokenAsync(ApplicationUser user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!)
        };
        var roles = await userManager.GetRolesAsync(user);
        var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        var allClaims = claims.Concat(roleClaims);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: allClaims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResponse(
            accessToken,
            "Bearer",
            expiresAt,
            ToResponse(user),
            roles.Order(StringComparer.Ordinal).ToArray());
    }

    private async Task<string> CreateRefreshTokenAsync(ApplicationUser user, string? sessionId)
    {
        sessionId ??= WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var token = $"{user.Id}.{sessionId}.{secret}";
        var tokenHash = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
            .ToString("O", CultureInfo.InvariantCulture);

        await SetRefreshTokenValueAsync(user, RefreshTokenHashName(sessionId), tokenHash);
        await SetRefreshTokenValueAsync(user, RefreshTokenExpiryName(sessionId), expiresAt);
        return token;
    }

    private async Task<(ApplicationUser User, string SessionId)?> ResolveRefreshTokenAsync(
        string? token,
        bool checkExpiry)
    {
        var parsedToken = ParseRefreshToken(token);
        if (parsedToken is null)
        {
            return null;
        }

        var (userId, sessionId, secret) = parsedToken.Value;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var storedHash = await userManager.GetAuthenticationTokenAsync(
            user,
            RefreshTokenProvider,
            RefreshTokenHashName(sessionId));
        if (!MatchesRefreshToken(secret, storedHash))
        {
            return null;
        }

        if (checkExpiry)
        {
            var storedExpiry = await userManager.GetAuthenticationTokenAsync(
                user,
                RefreshTokenProvider,
                RefreshTokenExpiryName(sessionId));
            if (!DateTimeOffset.TryParse(
                    storedExpiry,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var expiresAt) ||
                expiresAt <= DateTimeOffset.UtcNow)
            {
                await RemoveRefreshTokenAsync(user, sessionId);
                return null;
            }
        }

        return (user, sessionId);
    }

    private static (string UserId, string SessionId, string Secret)? ParseRefreshToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
        {
            return null;
        }

        var secretSeparator = token.LastIndexOf('.');
        var sessionSeparator = secretSeparator > 0 ? token.LastIndexOf('.', secretSeparator - 1) : -1;
        if (sessionSeparator <= 0 ||
            sessionSeparator == secretSeparator - 1 ||
            secretSeparator == token.Length - 1)
        {
            return null;
        }

        var sessionId = token[(sessionSeparator + 1)..secretSeparator];
        var secret = token[(secretSeparator + 1)..];
        try
        {
            if (WebEncoders.Base64UrlDecode(sessionId).Length != 16 ||
                WebEncoders.Base64UrlDecode(secret).Length != 32)
            {
                return null;
            }
        }
        catch (FormatException)
        {
            return null;
        }

        return (token[..sessionSeparator], sessionId, secret);
    }

    private static bool MatchesRefreshToken(string secret, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        try
        {
            var expectedHash = WebEncoders.Base64UrlDecode(storedHash);
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task SetRefreshTokenValueAsync(ApplicationUser user, string name, string value)
    {
        var result = await userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, name, value);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not store the refresh session: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    private async Task RemoveRefreshTokenAsync(ApplicationUser user, string sessionId)
    {
        foreach (var name in new[] { RefreshTokenHashName(sessionId), RefreshTokenExpiryName(sessionId) })
        {
            var result = await userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, name);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not revoke the refresh session: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    private static string RefreshTokenHashName(string sessionId) => $"{RefreshTokenHashPrefix}{sessionId}";

    private static string RefreshTokenExpiryName(string sessionId) => $"{RefreshTokenExpiryPrefix}{sessionId}";

    private static AuthUserResponse ToResponse(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FirstName, user.LastName);
}
