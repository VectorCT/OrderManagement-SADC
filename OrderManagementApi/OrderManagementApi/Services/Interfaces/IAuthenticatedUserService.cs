using System.Security.Claims;

namespace OrderManagementApi.Services.Interfaces;

public interface IAuthenticatedUserService
{
    Task<ClaimsPrincipal?> CreateApplicationPrincipalAsync(
        ClaimsPrincipal jwtPrincipal,
        CancellationToken cancellationToken);
}
