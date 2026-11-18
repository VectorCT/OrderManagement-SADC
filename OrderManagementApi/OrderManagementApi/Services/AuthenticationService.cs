using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataAccessLayer.Models;
using Microsoft.AspNetCore.Identity;
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
            return new LoginResult(null);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return new LoginResult(null);
        }

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

        return new LoginResult(new AccessTokenResponse(
            accessToken,
            "Bearer",
            expiresAt,
            ToResponse(user),
            roles.Order(StringComparer.Ordinal).ToArray()));
    }

    private static AuthUserResponse ToResponse(ApplicationUser user) =>
        new(user.Id, user.Email!, user.FirstName, user.LastName);
}
