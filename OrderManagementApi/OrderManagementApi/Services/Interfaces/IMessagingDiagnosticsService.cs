using OrderManagementApi.Contracts;

namespace OrderManagementApi.Services.Interfaces;

public interface IMessagingDiagnosticsService
{
    Task<MessagingDiagnosticsResponse> GetAsync(CancellationToken cancellationToken = default);
}
