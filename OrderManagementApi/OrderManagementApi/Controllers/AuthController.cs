using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderManagementApi.Contracts;
using OrderManagementApi.Services.Interfaces;

namespace OrderManagementApi.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : ApiControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            registerValidator,
            async (validatedRequest, ct) =>
            {
                var result = await authenticationService.RegisterAsync(validatedRequest, ct);
                if (!result.Succeeded)
                {
                    var statusCode = result.Conflict
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest;
                    return StatusCode(statusCode, ApiResponse.Create(
                        statusCode,
                        result.Conflict ? "An account with this email already exists." : "Registration failed.",
                        result.Errors,
                        HttpContext));
                }

                return StatusCode(StatusCodes.Status201Created, ApiResponse.Create(
                    StatusCodes.Status201Created,
                    "Account created successfully. Log in to receive an access token.",
                    result.User,
                    HttpContext));
            },
            cancellationToken);

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        CallValidatedRequestAsync(
            request,
            loginValidator,
            async (validatedRequest, ct) =>
            {
                var result = await authenticationService.LoginAsync(validatedRequest, ct);
                return result.Token is null
                    ? Unauthorized(ApiResponse.Create(
                        StatusCodes.Status401Unauthorized,
                        "Email or password is incorrect.",
                        null,
                        HttpContext))
                    : Ok(ApiResponse.Create(
                        StatusCodes.Status200OK,
                        "Login successful.",
                        result.Token,
                        HttpContext));
            },
            cancellationToken);
}
