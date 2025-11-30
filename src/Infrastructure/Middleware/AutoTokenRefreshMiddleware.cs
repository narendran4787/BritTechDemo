using System.IdentityModel.Tokens.Jwt;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware;

/// <summary>
/// Middleware that automatically refreshes access tokens when they're expired or about to expire.
/// This middleware intercepts requests with expired or expiring access tokens and automatically
/// refreshes them using a provided refresh token, returning new tokens in response headers.
/// </summary>
public class AutoTokenRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AutoTokenRefreshMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoTokenRefreshMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <param name="tokenService">The token service for refreshing tokens.</param>
    /// <param name="logger">The logger for recording token refresh operations.</param>
    public AutoTokenRefreshMiddleware(
        RequestDelegate next, 
        ITokenService tokenService,
        ILogger<AutoTokenRefreshMiddleware> logger)
    {
        _next = next;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to process the HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method:
    /// 1. Skips token refresh for authentication endpoints
    /// 2. Checks if the request has an Authorization header with a Bearer token
    /// 3. Checks if the access token is expired or expiring soon (within 5 minutes)
    /// 4. If expired and a refresh token is provided, automatically refreshes the tokens
    /// 5. Returns new tokens in response headers (X-New-Access-Token, X-New-Refresh-Token)
    /// 6. Updates the Authorization header for the current request with the new access token
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip token refresh for auth endpoints
        if (context.Request.Path.StartsWithSegments("/api/v1/auth"))
        {
            await _next(context);
            return;
        }

        // Check if request has Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            await _next(context);
            return;
        }

        var accessToken = authHeader.Substring("Bearer ".Length).Trim();
        var refreshToken = context.Request.Headers["X-Refresh-Token"].FirstOrDefault();

        // Check if access token is expired or about to expire (within 5 minutes)
        if (IsTokenExpiredOrExpiringSoon(accessToken) && !string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogInformation("Access token expired or expiring soon. Attempting automatic refresh.");

            var newTokens = _tokenService.RefreshToken(refreshToken);
            if (newTokens != null)
            {
                var (newAccessToken, newRefreshToken) = newTokens.Value;

                // Add new tokens to response headers
                context.Response.Headers.Append("X-New-Access-Token", newAccessToken);
                context.Response.Headers.Append("X-New-Refresh-Token", newRefreshToken);
                context.Response.Headers.Append("X-Token-Refreshed", "true");

                _logger.LogInformation("Tokens automatically refreshed successfully.");

                // Update the Authorization header for the current request
                context.Request.Headers["Authorization"] = $"Bearer {newAccessToken}";
            }
            else
            {
                _logger.LogWarning("Failed to refresh token automatically. Refresh token may be invalid or expired.");
            }
        }

        await _next(context);
    }

    /// <summary>
    /// Determines if a JWT token is expired or will expire within 5 minutes.
    /// </summary>
    /// <param name="token">The JWT access token to check.</param>
    /// <returns>
    /// True if the token is expired or will expire within 5 minutes, otherwise false.
    /// Returns true if the token cannot be parsed (assumed invalid/expired).
    /// </returns>
    /// <remarks>
    /// This method proactively refreshes tokens that are about to expire to prevent
    /// authentication failures during long-running operations. The 5-minute threshold
    /// provides a buffer for token expiration.
    /// </remarks>
    private bool IsTokenExpiredOrExpiringSoon(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return false;

            var jwtToken = handler.ReadJwtToken(token);
            var expirationTime = jwtToken.ValidTo;
            var timeUntilExpiration = expirationTime - DateTime.UtcNow;

            // Consider token expired if it's already expired or will expire within 5 minutes
            return timeUntilExpiration <= TimeSpan.FromMinutes(5);
        }
        catch
        {
            // If we can't parse the token, assume it's invalid/expired
            return true;
        }
    }
}

