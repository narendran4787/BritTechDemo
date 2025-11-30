namespace Infrastructure.Identity;

/// <summary>
/// Configuration options for JWT token generation and validation.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// The configuration section name for JWT options in appsettings.json.
    /// </summary>
    public const string SectionName = "Jwt";
    
    /// <summary>
    /// Gets or sets the issuer of the JWT token.
    /// This should match the issuer used during token validation.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the audience of the JWT token.
    /// This should match the audience used during token validation.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens.
    /// Must be at least 32 characters for HMAC-SHA256 algorithm.
    /// </summary>
    /// <remarks>
    /// In production, this should be a strong, randomly generated key stored securely
    /// (e.g., Azure Key Vault, AWS Secrets Manager, or environment variables).
    /// </remarks>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the lifetime of access tokens in minutes.
    /// Default is 15 minutes. Recommended: 15-60 minutes for production.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;
    
    /// <summary>
    /// Gets or sets the lifetime of refresh tokens in days.
    /// Default is 7 days. Recommended: 7-30 days for production.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 7;
}
