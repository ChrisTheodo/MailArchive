using MailArchive.Application.Auth;
using MailArchive.Application.Common;
using MailArchive.Application.Contracts.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MailArchive.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService service,
        IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _service.LoginAsync(request);

        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<LoginResponse>.Ok(result.Value!));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var result = await _service.RefreshAsync(request);

        if (!result.IsSuccess)
            return Unauthorized(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<LoginResponse>.Ok(result.Value!));
    }

    [AllowAnonymous]
    [HttpPost("refresh/revoke")]
    public async Task<IActionResult> RevokeRefreshToken(
        RefreshTokenRequest request)
    {
        var result = await _service.RevokeRefreshTokenAsync(request);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    [AllowAnonymous]
    [HttpGet("microsoft/login")]
    public IActionResult MicrosoftLogin()
    {
        var frontendUrl =
            _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";

        var properties = new AuthenticationProperties
        {
            RedirectUri = frontendUrl
        };

        return Challenge(properties, "Microsoft");
    }

    // Current authenticated user
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            displayName = User.FindFirstValue(ClaimTypes.Name),
            role = User.FindFirstValue(ClaimTypes.Role),
            isAdmin = User.IsInRole("Admin")
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? request)
    {
        var result = await _service.LogoutAsync(
            request ?? new LogoutRequest(null));

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<string>.Fail(result.Error!));

        return Ok(ApiResponse<string>.Ok(result.Value!));
    }

    [Authorize]
    [HttpPost("microsoft/logout")]
    public async Task<IActionResult> MicrosoftLogout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }
}