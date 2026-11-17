using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "Orders.Admin")]
public sealed class AdminUsersController(
    IAdminUserManagementService userManagementService,
    IAuthenticationService authenticationService,
    IValidator<RegisterRequest> registerRequestValidator,
    IValidator<UpdateAdminUserRequest> updateRequestValidator) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var users = await userManagementService.ListAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse.Create(StatusCodes.Status200OK, "Users retrieved successfully.", new { items = users, page, pageSize }, HttpContext));
    }

    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string userId, CancellationToken cancellationToken)
    {
        var user = await userManagementService.GetAsync(userId, cancellationToken);
        return user is null
            ? NotFound(ApiResponse.Create(StatusCodes.Status404NotFound, "User was not found.", null, HttpContext))
            : Ok(ApiResponse.Create(StatusCodes.Status200OK, "User retrieved successfully.", user, HttpContext));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Create([FromBody] RegisterRequest request, CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            registerRequestValidator,
            async (validatedRequest, ct) =>
            {
                var result = await authenticationService.RegisterAsync(validatedRequest, ct);
                if (!result.Succeeded)
                {
                    var statusCode = result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
                    return StatusCode(statusCode, ApiResponse.Create(statusCode, "User could not be created.", result.Errors, HttpContext));
                }

                var created = result.User!;
                var adminUser = await userManagementService.GetAsync(created.Id, ct);
                var response = ApiResponse.Create(
                    StatusCodes.Status201Created,
                    "User created successfully.",
                    adminUser,
                    HttpContext);
                return CreatedAtAction(nameof(Get), new { userId = created.Id }, response);
            },
            cancellationToken);

    [HttpPut("{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Update(
        string userId,
        [FromBody] UpdateAdminUserRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            updateRequestValidator,
            async (validatedRequest, ct) =>
            {
                var result = await userManagementService.UpdateAsync(userId, validatedRequest, ct);
                if (!result.UserFound)
                {
                    return NotFound(ApiResponse.Create(StatusCodes.Status404NotFound, "User was not found.", null, HttpContext));
                }

                if (!result.Succeeded)
                {
                    var statusCode = result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
                    return StatusCode(statusCode, ApiResponse.Create(statusCode, "User could not be updated.", result.Errors, HttpContext));
                }

                return Ok(ApiResponse.Create(StatusCodes.Status200OK, "User updated successfully.", result.User, HttpContext));
            },
            cancellationToken);

    [HttpDelete("{userId}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string userId, CancellationToken cancellationToken)
    {
        if (string.Equals(User.FindFirstValue(ClaimTypes.NameIdentifier), userId, StringComparison.Ordinal))
        {
            return Conflict(ApiResponse.Create(StatusCodes.Status409Conflict, "You cannot delete your own account while signed in.", null, HttpContext));
        }

        var result = await userManagementService.DeleteAsync(userId, cancellationToken);
        if (!result.UserFound)
        {
            return NotFound(ApiResponse.Create(StatusCodes.Status404NotFound, "User was not found.", null, HttpContext));
        }

        if (!result.Succeeded)
        {
            var statusCode = result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            return StatusCode(statusCode, ApiResponse.Create(statusCode, "User could not be deleted.", result.Errors, HttpContext));
        }

        return NoContent();
    }
}
