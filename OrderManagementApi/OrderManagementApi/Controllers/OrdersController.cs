using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;
using OrderManagementApi.Services.Models;
using OrderManagementApi.Services.Validation;

namespace OrderManagementApi.Controllers;

/// <summary>Creates, queries, and transitions orders.</summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IOrderService orderService,
    IValidator<CreateOrderRequest> createOrderRequestValidator,
    IValidator<TransitionOrderStatusRequest> transitionOrderStatusRequestValidator) : ApiControllerBase
{
    /// <summary>Lists orders with optional customer and status filters.</summary>
    [HttpGet]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? customerId,
        [FromQuery] DataAccessLayer.Models.OrderStatus? status,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(sort) && !sort.Equals("date", StringComparison.OrdinalIgnoreCase) && !sort.Equals("total", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse.Create(
                StatusCodes.Status400BadRequest,
                "Sort must be either 'date' or 'total'.",
                new { sort = new[] { "Sort must be either 'date' or 'total'." } },
                HttpContext));
        }

        var result = await orderService.ListAsync(customerId, status, page, pageSize, sort, cancellationToken);
        if (result.IsFailure)
        {
            return ServiceFailure(result.Error);
        }

        return Ok(ApiResponse.Create(
            StatusCodes.Status200OK,
            "Orders retrieved successfully.",
            new { items = result.Value, page, pageSize },
            HttpContext));
    }

    /// <summary>Gets an order and its line items by identifier.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await orderService.GetAsync(id, cancellationToken);
        return result.IsSuccess
            ? Ok(ApiResponse.Create(StatusCodes.Status200OK, "Order retrieved successfully.", result.Value, HttpContext))
            : ServiceFailure(result.Error);
    }

    /// <summary>Creates an order and returns its resource location.</summary>
    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            createOrderRequestValidator,
            async (validatedRequest, ct) =>
            {
                var result = await orderService.CreateAsync(validatedRequest, ct);
                if (result.IsFailure)
                {
                    return ServiceFailure(result.Error);
                }

                var response = ApiResponse.Create(
                    StatusCodes.Status201Created,
                    "Order created successfully.",
                    result.Value,
                    HttpContext);
                return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, response);
            },
            cancellationToken);

    /// <summary>Transitions an order's status using the required idempotency key.</summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "Orders.Admin")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> TransitionStatus(
        Guid id,
        [FromBody] TransitionOrderStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            transitionOrderStatusRequestValidator,
            async (validatedRequest, ct) =>
            {
                if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 128)
                {
                    return BadRequest(ApiResponse.Create(
                        StatusCodes.Status400BadRequest,
                        "A non-empty Idempotency-Key header of at most 128 characters is required.",
                        new { idempotencyKey = new[] { "A non-empty key of at most 128 characters is required." } },
                        HttpContext));
                }

                var result = await orderService.TransitionStatusAsync(
                    id,
                    validatedRequest.Status,
                    idempotencyKey.Trim(),
                    ct);
                return result.IsSuccess
                    ? Ok(ApiResponse.Create(StatusCodes.Status200OK, "Order status updated successfully.", result.Value, HttpContext))
                    : ServiceFailure(result.Error);
            },
            cancellationToken);

    private ObjectResult ServiceFailure(string error)
    {
        var statusCode = error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? StatusCodes.Status404NotFound
            : error.Contains("currency", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status422UnprocessableEntity
                : StatusCodes.Status409Conflict;
        return StatusCode(statusCode, ApiResponse.Create(statusCode, error, null, HttpContext));
    }
}