using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface IAuthenticationService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
