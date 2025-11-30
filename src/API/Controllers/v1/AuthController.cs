using Asp.Versioning;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Solution.API.Controllers.v1;

/// <summary>
/// Controller for handling authentication operations including login and token refresh.
/// </summary>
[ApiController]
[RequireHttps]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    private readonly ITokenService _tokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="tokenService">The token service for creating and refreshing JWT tokens.</param>
    public AuthController(ITokenService tokenService)
    {
        _tokenService = tokenService;
    }

    /// <summary>
    /// Authenticates a user and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">The login request containing username and password.</param>
    /// <returns>
    /// A <see cref="TokenResponse"/> containing the access token and refresh token on success.
    /// Returns 400 Bad Request if username or password is missing.
    /// </returns>
    /// <remarks>
    /// This endpoint accepts any username/password combination for development purposes.
    /// In production, this should validate credentials against a user database.
    /// </remarks>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Token([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Unauthorized(new { error = "Username and password are required" });
        }

        var (access, refresh) = _tokenService.CreateTokens(
            Guid.NewGuid().ToString(), 
            request.Username, 
            new[] { "Admin" });
        
        return Ok(new TokenResponse(access, refresh));
    }
    
    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="request">The refresh token request containing the refresh token.</param>
    /// <returns>
    /// A <see cref="TokenResponse"/> containing new access and refresh tokens on success.
    /// Returns 400 Bad Request if refresh token is missing.
    /// Returns 401 Unauthorized if refresh token is invalid or expired.
    /// </returns>
    /// <remarks>
    /// Refresh tokens are single-use. After a successful refresh, the old refresh token
    /// is invalidated and a new one is issued. This prevents token replay attacks.
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Refresh([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "Refresh token is required" });
        }

        var result = _tokenService.RefreshToken(request.RefreshToken);
        if (result == null)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token" });
        }

        var (access, refresh) = result.Value;
        return Ok(new TokenResponse(access, refresh));
    }
}

/// <summary>
/// Represents a login request with username and password.
/// </summary>
/// <param name="Username">The username for authentication.</param>
/// <param name="Password">The password for authentication.</param>
public record LoginRequest(string Username, string Password);

/// <summary>
/// Represents a refresh token request.
/// </summary>
/// <param name="RefreshToken">The refresh token to use for obtaining new access and refresh tokens.</param>
public record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// Represents a token response containing both access and refresh tokens.
/// </summary>
/// <param name="AccessToken">The JWT access token for API authorization.</param>
/// <param name="RefreshToken">The refresh token for obtaining new access tokens.</param>
public record TokenResponse(string AccessToken, string RefreshToken);
