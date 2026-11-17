using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = "Orders.Admin")]
public sealed class RolesController(
    IRoleManagementService roleManagementService,
    IValidator<AssignRolesRequest> assignRolesRequestValidator) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAvailableRoles(CancellationToken cancellationToken)
    {
        var roles = await roleManagementService.GetAvailableRolesAsync(cancellationToken);
        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Available roles retrieved successfully.",
            roles,
            HttpContext));
    }

    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoles(string userId, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.GetRolesAsync(userId, cancellationToken);
        return result.UserFound
            ? Ok(ApiResponse.Create(
                StatusCodes.Status200OK,
                "User roles retrieved successfully.",
                result.Roles,
                HttpContext))
            : NotFound(ApiResponse.Create(
                StatusCodes.Status404NotFound,
                "User was not found.",
                null,
                HttpContext));
    }

    [HttpPost("users/{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> AssignRoles(
        string userId,
        [FromBody] AssignRolesRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            assignRolesRequestValidator,
            async (validatedRequest, ct) =>
            {
                var result = await roleManagementService.AssignRolesAsync(userId, validatedRequest.Roles, ct);
                if (!result.UserFound)
                {
                    return NotFound(ApiResponse.Create(
                        StatusCodes.Status404NotFound,
                        "User was not found.",
                        null,
                        HttpContext));
                }

                if (!result.Succeeded)
                {
                    var statusCode = result.Errors.Values.Any(messages =>
                        messages.Any(message => message == "The requested role does not exist."))
                        ? StatusCodes.Status400BadRequest
                        : StatusCodes.Status409Conflict;
                    return StatusCode(statusCode, ApiResponse.Create(
                        statusCode,
                        "One or more roles could not be assigned.",
                        result.Errors,
                        HttpContext));
                }

                return Ok(ApiResponse.Create(
                    StatusCodes.Status200OK,
                    "Roles assigned successfully.",
                    result.Roles,
                    HttpContext));
            },
            cancellationToken);
}
