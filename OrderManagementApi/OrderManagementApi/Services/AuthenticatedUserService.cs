using System.Security.Claims;
using DataAccessLayer.Contexts;
using Microsoft.EntityFrameworkCore;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Services;

public sealed class AuthenticatedUserService(AppDbContext dbContext) : IAuthenticatedUserService
{
    public async Task<ClaimsPrincipal?> CreateApplicationPrincipalAsync(
        ClaimsPrincipal jwtPrincipal,
        CancellationToken cancellationToken)
    {
        var subject = jwtPrincipal.FindFirst("sub")?.Value
            ?? jwtPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == subject, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == user.Id
            select role.Name)
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new("sub", user.Id),
            new(ClaimTypes.NameIdentifier, user.Id)
        };
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.UserName));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        claims.AddRange(roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role!)));

        var identity = new ClaimsIdentity(
            claims,
            jwtPrincipal.Identity?.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
