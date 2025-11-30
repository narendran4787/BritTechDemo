using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Identity;

/// <summary>
/// Service interface for creating, refreshing, and validating JWT tokens.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Creates a new access token and refresh token pair for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="username">The username of the user.</param>
    /// <param name="roles">Optional list of roles to include in the token claims.</param>
    /// <returns>A tuple containing the access token and refresh token.</returns>
    (string accessToken, string refreshToken) CreateTokens(string userId, string username, IEnumerable<string>? roles = null);
    
    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use for obtaining new tokens.</param>
    /// <returns>
    /// A tuple containing new access and refresh tokens if the refresh token is valid,
    /// otherwise null if the refresh token is invalid or expired.
    /// </returns>
    /// <remarks>
    /// This method implements one-time use refresh tokens. After a successful refresh,
    /// the old refresh token is invalidated and cannot be used again.
    /// </remarks>
    (string accessToken, string refreshToken)? RefreshToken(string refreshToken);
    
    /// <summary>
    /// Validates whether a refresh token is valid and not expired.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <returns>True if the refresh token is valid and not expired, otherwise false.</returns>
    bool ValidateRefreshToken(string refreshToken);
}

/// <summary>
/// Internal class representing refresh token data stored in memory.
/// </summary>
internal class RefreshTokenData
{
    /// <summary>
    /// Gets or sets the user identifier associated with the refresh token.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the username associated with the refresh token.
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the list of roles associated with the refresh token.
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the expiration date and time of the refresh token.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Service for creating, refreshing, and validating JWT tokens.
/// Implements in-memory storage for refresh tokens with automatic cleanup of expired tokens.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly Dictionary<string, RefreshTokenData> _refreshTokens = new();
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenService"/> class.
    /// </summary>
    /// <param name="options">The JWT configuration options.</param>
    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    /// <summary>
    /// Creates a new access token and refresh token pair for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="username">The username of the user.</param>
    /// <param name="roles">Optional list of roles to include in the token claims.</param>
    /// <returns>A tuple containing the access token and refresh token.</returns>
    /// <remarks>
    /// The access token is a JWT signed with HMAC-SHA256 containing user claims.
    /// The refresh token is a cryptographically secure random 64-byte value encoded as Base64.
    /// Refresh tokens are stored in-memory and will be lost on server restart.
    /// </remarks>
    public (string accessToken, string refreshToken) CreateTokens(string userId, string username, IEnumerable<string>? roles = null)
    {
        var rolesList = roles?.ToList() ?? new List<string>();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username)
        };
        if (rolesList.Any())
        {
            claims.AddRange(rolesList.Select(r => new Claim(ClaimTypes.Role, r)));
        }
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds
        );
        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        
        // Store refresh token
        lock (_lock)
        {
            _refreshTokens[refresh] = new RefreshTokenData
            {
                UserId = userId,
                Username = username,
                Roles = rolesList,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
            };
            
            // Clean up expired tokens
            CleanupExpiredTokens();
        }
        
        return (access, refresh);
    }

    /// <summary>
    /// Refreshes an expired access token using a valid refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use for obtaining new tokens.</param>
    /// <returns>
    /// A tuple containing new access and refresh tokens if the refresh token is valid,
    /// otherwise null if the refresh token is invalid or expired.
    /// </returns>
    /// <remarks>
    /// This method implements one-time use refresh tokens for security. After a successful refresh,
    /// the old refresh token is removed from storage and cannot be reused. This prevents token
    /// replay attacks. The method is thread-safe and performs automatic cleanup of expired tokens.
    /// </remarks>
    public (string accessToken, string refreshToken)? RefreshToken(string refreshToken)
    {
        lock (_lock)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out var tokenData))
            {
                return null;
            }

            // Check if token is expired
            if (tokenData.ExpiresAt <= DateTime.UtcNow)
            {
                _refreshTokens.Remove(refreshToken);
                return null;
            }

            // Remove old refresh token (one-time use for security)
            _refreshTokens.Remove(refreshToken);

            // Create new tokens
            return CreateTokens(tokenData.UserId, tokenData.Username, tokenData.Roles);
        }
    }

    /// <summary>
    /// Validates whether a refresh token is valid and not expired.
    /// </summary>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <returns>True if the refresh token is valid and not expired, otherwise false.</returns>
    /// <remarks>
    /// This method checks if the refresh token exists in storage and has not expired.
    /// Expired tokens are automatically removed from storage during validation.
    /// </remarks>
    public bool ValidateRefreshToken(string refreshToken)
    {
        lock (_lock)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out var tokenData))
            {
                return false;
            }

            if (tokenData.ExpiresAt <= DateTime.UtcNow)
            {
                _refreshTokens.Remove(refreshToken);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Removes expired refresh tokens from the in-memory storage.
    /// </summary>
    /// <remarks>
    /// This method is called automatically during token creation to prevent memory leaks.
    /// It removes all refresh tokens that have passed their expiration time.
    /// </remarks>
    private void CleanupExpiredTokens()
    {
        var expiredKeys = _refreshTokens
            .Where(kvp => kvp.Value.ExpiresAt <= DateTime.UtcNow)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _refreshTokens.Remove(key);
        }
    }
}
