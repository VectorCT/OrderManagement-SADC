using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;

namespace OrderManagementApi.Controllers;

/// <summary>Creates and queries customer records.</summary>
[ApiController]
[Route("api/customers")]
public sealed class CustomersController(
    ICustomerService customerService,
    IValidator<CreateCustomerRequest> createCustomerRequestValidator) : ApiControllerBase
{
    /// <summary>Searches customers using the standard one-based, bounded pagination scheme.</summary>
    [HttpGet]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await customerService.SearchAsync(search, page, pageSize, cancellationToken);
        if (result.IsFailure)
        {
            return ServiceFailure(result.Error);
        }

        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Customers retrieved successfully.",
            new { items = result.Value, page, pageSize },
            HttpContext));
    }

    /// <summary>Gets a customer by its identifier.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerService.GetAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse.Create(StatusCodes.Status200OK, "Customer retrieved successfully.", result.Value, HttpContext))
            : ServiceFailure(result.Error);
    }

    /// <summary>Creates a customer.</summary>
    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            createCustomerRequestValidator,
            async (validatedRequest, ct) =>
            {
                var result = await customerService.CreateAsync(validatedRequest, ct);
                if (result.IsFailure)
                {
                    return ServiceFailure(result.Error);
                }

                var response = ApiResponse.Create(
                    StatusCodes.Status201Created,
                    "Customer created successfully.",
                    result.Value,
                    HttpContext);
                return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, response);
            },
            cancellationToken);

    private ObjectResult ServiceFailure(string error)
    {
        var statusCode = error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status409Conflict;
        return StatusCode(statusCode, ApiResponse.Create(statusCode, error, null, HttpContext));
    }
}