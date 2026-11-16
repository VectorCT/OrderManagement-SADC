using CSharpFunctionalExtensions;
using OrderManagementApi.Services.Models;

namespace OrderManagementApi.Services.Interfaces;

public interface ICustomerService
{
    Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<Result<CustomerDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CustomerDto>>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default);
}
