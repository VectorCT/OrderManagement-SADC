using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Controllers;

/// <summary>Provides operational diagnostics for order event processing.</summary>
[ApiController]
[Route("api/admin/messaging")]
[Authorize(Policy = "Orders.Admin")]
public sealed class MessagingDiagnosticsController(
    IMessagingDiagnosticsService diagnosticsService) : ControllerBase
{
    /// <summary>Gets order, outbox, processed-message, retry, and dead-letter queue status.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var diagnostics = await diagnosticsService.GetAsync(cancellationToken);
        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Messaging diagnostics retrieved successfully.",
            diagnostics,
            HttpContext));
    }
}
