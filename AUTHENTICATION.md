# Authentication Flow and Setup Guide

## Table of Contents
1. [Overview](#overview)
2. [High-Level Authentication Flow](#high-level-authentication-flow)
3. [Architecture Components](#architecture-components)
4. [Setup Instructions](#setup-instructions)
5. [Configuration](#configuration)
6. [API Endpoints](#api-endpoints)
7. [Usage Examples](#usage-examples)
8. [Token Lifecycle](#token-lifecycle)
9. [Security Considerations](#security-considerations)
10. [Troubleshooting](#troubleshooting)

---

## Overview

This application implements a JWT-based authentication system with automatic token refresh capabilities. The system uses:

- **Access Tokens**: Short-lived JWT tokens (default: 60 minutes) for API authorization
- **Refresh Tokens**: Long-lived tokens (default: 7 days) stored in-memory for token renewal
- **Automatic Refresh**: Middleware that automatically refreshes expired access tokens
- **One-Time Use**: Refresh tokens are single-use for enhanced security

---

## High-Level Authentication Flow

### 1. Initial Login Flow

```
┌─────────┐                    ┌──────────┐                    ┌─────────────┐
│ Client  │                    │   API    │                    │ TokenService│
└────┬────┘                    └────┬─────┘                    └──────┬──────┘
     │                               │                                 │
     │ POST /api/v1/auth/token      │                                 │
     │ {username, password}          │                                 │
     ├──────────────────────────────>│                                 │
     │                               │                                 │
     │                               │ CreateTokens(userId, username)  │
     │                               ├───────────────────────────────>│
     │                               │                                 │
     │                               │ Generate JWT Access Token       │
     │                               │ Generate Refresh Token          │
     │                               │ Store Refresh Token (in-memory) │
     │                               │<───────────────────────────────┤
     │                               │                                 │
     │ {accessToken, refreshToken}   │                                 │
     │<──────────────────────────────┤                                 │
     │                               │                                 │
```

### 2. Automatic Token Refresh Flow

```
┌─────────┐                    ┌──────────────────┐              ┌─────────────┐
│ Client  │                    │ AutoTokenRefresh │              │ TokenService│
│         │                    │   Middleware     │              │             │
└────┬────┘                    └────┬─────────────┘              └──────┬──────┘
     │                               │                                    │
     │ API Request with:             │                                    │
     │ - Authorization: Bearer <token>│                                    │
     │ - X-Refresh-Token: <refresh>   │                                    │
     ├───────────────────────────────>│                                    │
     │                               │                                    │
     │                               │ Check if token expired/expiring    │
     │                               │ (within 5 minutes)                │
     │                               │                                    │
     │                               │ RefreshToken(refreshToken)         │
     │                               ├───────────────────────────────────>│
     │                               │                                    │
     │                               │ Validate refresh token             │
     │                               │ Remove old refresh token           │
     │                               │ Generate new access + refresh      │
     │                               │<───────────────────────────────────┤
     │                               │                                    │
     │ Response Headers:              │                                    │
     │ - X-New-Access-Token           │                                    │
     │ - X-New-Refresh-Token          │                                    │
     │ - X-Token-Refreshed: true      │                                    │
     │<───────────────────────────────┤                                    │
     │                               │                                    │
```

### 3. Manual Token Refresh Flow

```
┌─────────┐                    ┌──────────┐                    ┌─────────────┐
│ Client  │                    │   API    │                    │ TokenService│
└────┬────┘                    └────┬─────┘                    └──────┬──────┘
     │                               │                                 │
     │ POST /api/v1/auth/refresh     │                                 │
     │ {refreshToken}                │                                 │
     ├──────────────────────────────>│                                 │
     │                               │                                 │
     │                               │ RefreshToken(refreshToken)      │
     │                               ├───────────────────────────────>│
     │                               │                                 │
     │                               │ Validate & remove old token    │
     │                               │ Generate new tokens             │
     │                               │<───────────────────────────────┤
     │                               │                                 │
     │ {accessToken, refreshToken}   │                                 │
     │<──────────────────────────────┤                                 │
     │                               │                                 │
```

---

## Architecture Components

### 1. **TokenService** (`Infrastructure/Identity/TokenService.cs`)
- **Responsibility**: Token generation, validation, and refresh token management
- **Storage**: In-memory dictionary for refresh tokens (thread-safe)
- **Features**:
  - Creates JWT access tokens with user claims
  - Generates cryptographically secure refresh tokens
  - Manages refresh token lifecycle (expiration, cleanup)
  - Implements one-time use refresh tokens

### 2. **AuthController** (`API/Controllers/v1/AuthController.cs`)
- **Endpoints**:
  - `POST /api/v1/auth/token` - Initial login
  - `POST /api/v1/auth/refresh` - Manual token refresh

### 3. **AutoTokenRefreshMiddleware** (`Infrastructure/Middleware/AutoTokenRefreshMiddleware.cs`)
- **Responsibility**: Automatic token refresh before request processing
- **Behavior**:
  - Intercepts requests with expired/expiring access tokens
  - Automatically refreshes tokens if valid refresh token provided
  - Returns new tokens in response headers
  - Updates Authorization header for current request

### 4. **JWT Configuration** (`Infrastructure/Identity/JwtOptions.cs`)
- Configurable token expiration times
- Issuer and audience validation
- Signing key management

---

## Setup Instructions

### Prerequisites
- Docker Desktop (or Docker Engine + Docker Compose)
- Git (to clone the repository)

### Step 1: Generate HTTPS Certificate

The API requires an HTTPS certificate for secure communication. Generate a development certificate:

**Option A: Use the provided script (recommended)**

```bash
# Make the script executable (if not already)
chmod +x generate-dev-cert.sh

# Run the certificate generation script
./generate-dev-cert.sh
```

**Option B: Manual generation**

```bash
# Create certs directory if it doesn't exist
mkdir -p certs

# Generate a self-signed certificate
openssl req -x509 -newkey rsa:4096 -keyout certs/dev-cert-key.pem -out certs/dev-cert.pem -days 365 -nodes -subj "/CN=localhost"

# Convert to PKCS#12 format (required by Kestrel)
openssl pkcs12 -export -out certs/dev-cert.pfx -inkey certs/dev-cert-key.pem -in certs/dev-cert.pem -password pass:dev-cert-password -name "dev-cert"
```

**Note**: 
- The certificate will be created at `certs/dev-cert.pfx` with password `dev-cert-password`
- For production, use certificates from a trusted Certificate Authority (CA)
- The certificate is included in the Docker image during build

### Step 2: Configure Environment Variables (Optional)

Create a `.env` file in the project root to customize settings:

```bash
# Database Configuration
DB_USERNAME=sa
DB_PASSWORD=YourStrong@Password123!
SA_PASSWORD=YourStrong@Password123!

# JWT Configuration
JWT_KEY=your-super-secret-key-minimum-32-characters-long
JWT_ISSUER=products-api
JWT_AUDIENCE=products-api-clients
JWT_ACCESS_TOKEN_MINUTES=60
JWT_REFRESH_TOKEN_DAYS=7
```

**Important Security Notes**:
- **JWT Key**: Must be at least 32 characters for HS256 algorithm
- **Production**: Use a strong, randomly generated key (e.g., 64+ characters)
- **Key Management**: Store production keys securely (Azure Key Vault, AWS Secrets Manager, etc.)
- **Database Password**: Use a strong password meeting SQL Server complexity requirements

### Step 3: Update docker-compose.yml for JWT Configuration

Edit `docker-compose.yml` to add JWT environment variables to the API service:

```yaml
api:
  environment:
    # ... existing environment variables ...
    - Jwt__Key=${JWT_KEY:-super_secret_development_key_change_me}
    - Jwt__Issuer=${JWT_ISSUER:-products-api}
    - Jwt__Audience=${JWT_AUDIENCE:-products-api-clients}
    - Jwt__AccessTokenMinutes=${JWT_ACCESS_TOKEN_MINUTES:-60}
    - Jwt__RefreshTokenDays=${JWT_REFRESH_TOKEN_DAYS:-7}
```

### Step 4: Configure CORS (if needed)

Update `docker-compose.yml` to add CORS environment variables:

```yaml
api:
  environment:
    # ... existing environment variables ...
    - Cors__AllowedOrigins__0=https://your-frontend-domain.com
    - Cors__AllowedHeaders__0=Content-Type
    - Cors__AllowedHeaders__1=Authorization
    - Cors__AllowedHeaders__2=X-Request-Id
    - Cors__AllowedHeaders__3=X-Refresh-Token
```

Or update `src/API/appsettings.json` before building the Docker image.

### Step 5: Build and Start Services

```bash
# Build and start all services (SQL Server + API)
docker compose up --build

# Or run in detached mode
docker compose up --build -d
```

This will:
1. Build the API Docker image
2. Start SQL Server container
3. Wait for SQL Server to be healthy
4. Start the API container
5. Create the database automatically on first startup

### Step 6: Verify Setup

Check that services are running:

```bash
# Check container status
docker compose ps

# View API logs
docker compose logs api

# View SQL Server logs
docker compose logs sqlserver
```

Test the authentication endpoint:

```bash
# Test login endpoint
curl -X POST https://localhost:8443/api/v1/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser", "password": "testpass"}' \
  -k
```

**Note**: The `-k` flag is required for self-signed certificates in development.

Expected response:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-refresh-token..."
}
```

### Step 7: Access Swagger UI (Development)

The API includes Swagger UI for testing endpoints:

```
https://localhost:8443/swagger
```

**Note**: You may need to accept the self-signed certificate in your browser.

### Common Docker Commands

```bash
# Stop all services
docker compose down

# Stop and remove volumes (clears database data)
docker compose down -v

# Rebuild and restart
docker compose up --build --force-recreate

# View real-time logs
docker compose logs -f api

# Execute commands in API container
docker compose exec api bash

# Execute commands in SQL Server container
docker compose exec sqlserver bash

# Connect to SQL Server
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStrong@Password123!' -C
```

---

## Configuration

### JWT Options

| Setting | Default | Description |
|---------|---------|-------------|
| `Issuer` | `products-api` | Token issuer identifier |
| `Audience` | `products-api-clients` | Token audience identifier |
| `Key` | (required) | Secret key for signing tokens (min 32 chars) |
| `AccessTokenMinutes` | 60 | Access token lifetime in minutes |
| `RefreshTokenDays` | 7 | Refresh token lifetime in days |

### Environment Variables

For production, override settings via environment variables:

```bash
export Jwt__Key="your-production-secret-key"
export Jwt__AccessTokenMinutes=15
export Jwt__RefreshTokenDays=30
```

### Docker Configuration

The application is configured to run in Docker using `docker-compose.yml`. Configuration is done via environment variables:

**JWT Configuration in docker-compose.yml:**

```yaml
api:
  environment:
    # JWT Settings
    - Jwt__Key=${JWT_KEY:-super_secret_development_key_change_me}
    - Jwt__Issuer=${JWT_ISSUER:-products-api}
    - Jwt__Audience=${JWT_AUDIENCE:-products-api-clients}
    - Jwt__AccessTokenMinutes=${JWT_ACCESS_TOKEN_MINUTES:-60}
    - Jwt__RefreshTokenDays=${JWT_REFRESH_TOKEN_DAYS:-7}
    
    # Database Settings
    - UseSqlServer=true
    - DB_USERNAME=${DB_USERNAME:-sa}
    - DB_PASSWORD=${DB_PASSWORD:-YourStrong@Password123!}
    - ConnectionStrings__DefaultConnection=Server=sqlserver,1433;Database=ProductsDb;User Id=${DB_USERNAME:-sa};Password=${DB_PASSWORD:-YourStrong@Password123!};TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=True;Connection Timeout=60;Pooling=true
    
    # HTTPS Settings
    - ASPNETCORE_URLS=https://+:8443
    - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/dev-cert.pfx
    - ASPNETCORE_Kestrel__Certificates__Default__Password=dev-cert-password
```

**Using .env file for configuration:**

Create a `.env` file in the project root:

```bash
# JWT Configuration
JWT_KEY=your-production-secret-key-minimum-32-characters
JWT_ISSUER=products-api
JWT_AUDIENCE=products-api-clients
JWT_ACCESS_TOKEN_MINUTES=15
JWT_REFRESH_TOKEN_DAYS=30

# Database Configuration
DB_USERNAME=sa
DB_PASSWORD=YourStrong@Password123!
SA_PASSWORD=YourStrong@Password123!
```

Docker Compose will automatically load variables from the `.env` file.

---

## API Endpoints

### 1. Login (Get Tokens)

**Endpoint**: `POST /api/v1/auth/token`

**Request**:
```json
{
  "username": "string",
  "password": "string"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "base64-encoded-token..."
}
```

**Error Responses**:
- `400 Bad Request`: Missing username or password
- `401 Unauthorized`: Invalid credentials

### 2. Refresh Tokens

**Endpoint**: `POST /api/v1/auth/refresh`

**Request**:
```json
{
  "refreshToken": "base64-encoded-refresh-token"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "new-access-token...",
  "refreshToken": "new-refresh-token..."
}
```

**Error Responses**:
- `400 Bad Request`: Missing refresh token
- `401 Unauthorized`: Invalid or expired refresh token

### 3. Protected Endpoints

All endpoints under `/api/v1/products` and `/api/v1/items` require authentication:

**Headers Required**:
```
Authorization: Bearer <access-token>
X-Refresh-Token: <refresh-token> (optional, for auto-refresh)
```

---

## Usage Examples

### Example 1: Basic Authentication Flow

```javascript
// 1. Login
const loginResponse = await fetch('https://api.example.com/api/v1/auth/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    username: 'user@example.com',
    password: 'securePassword123'
  })
});

const { accessToken, refreshToken } = await loginResponse.json();

// 2. Store tokens securely
localStorage.setItem('accessToken', accessToken);
localStorage.setItem('refreshToken', refreshToken);

// 3. Use access token for API calls
const apiResponse = await fetch('https://api.example.com/api/v1/products', {
  headers: {
    'Authorization': `Bearer ${accessToken}`,
    'X-Refresh-Token': refreshToken  // Optional, enables auto-refresh
  }
});

// 4. Check for new tokens in response headers
const newAccessToken = apiResponse.headers.get('X-New-Access-Token');
const newRefreshToken = apiResponse.headers.get('X-New-Refresh-Token');

if (newAccessToken && newRefreshToken) {
  localStorage.setItem('accessToken', newAccessToken);
  localStorage.setItem('refreshToken', newRefreshToken);
}
```

### Example 2: Automatic Token Refresh Handling

```javascript
async function makeAuthenticatedRequest(url, options = {}) {
  let accessToken = localStorage.getItem('accessToken');
  let refreshToken = localStorage.getItem('refreshToken');

  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      'Authorization': `Bearer ${accessToken}`,
      'X-Refresh-Token': refreshToken
    }
  });

  // Check if tokens were refreshed
  if (response.headers.get('X-Token-Refreshed') === 'true') {
    const newAccessToken = response.headers.get('X-New-Access-Token');
    const newRefreshToken = response.headers.get('X-New-Refresh-Token');
    
    localStorage.setItem('accessToken', newAccessToken);
    localStorage.setItem('refreshToken', newRefreshToken);
  }

  return response;
}
```

### Example 3: Manual Token Refresh

```javascript
async function refreshTokens() {
  const refreshToken = localStorage.getItem('refreshToken');
  
  const response = await fetch('https://api.example.com/api/v1/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken })
  });

  if (response.ok) {
    const { accessToken, refreshToken: newRefreshToken } = await response.json();
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', newRefreshToken);
    return true;
  }
  
  // Refresh token expired - redirect to login
  localStorage.removeItem('accessToken');
  localStorage.removeItem('refreshToken');
  window.location.href = '/login';
  return false;
}
```

### Example 4: cURL Examples

```bash
# Login
curl -X POST https://localhost:8443/api/v1/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser", "password": "testpass"}' \
  -k

# Use access token
curl -X GET https://localhost:8443/api/v1/products \
  -H "Authorization: Bearer <access-token>" \
  -H "X-Refresh-Token: <refresh-token>" \
  -k

# Manual refresh
curl -X POST https://localhost:8443/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken": "<refresh-token>"}' \
  -k
```

---

## Token Lifecycle

### Access Token
1. **Created**: On login or token refresh
2. **Lifetime**: 60 minutes (configurable)
3. **Usage**: Included in `Authorization: Bearer <token>` header
4. **Expiration**: Automatically refreshed if refresh token provided
5. **Validation**: Validated on every authenticated request

### Refresh Token
1. **Created**: On login or token refresh
2. **Lifetime**: 7 days (configurable)
3. **Storage**: In-memory dictionary (server-side)
4. **Usage**: 
   - Sent in `X-Refresh-Token` header for auto-refresh
   - Or used in `/api/v1/auth/refresh` endpoint
5. **One-Time Use**: Consumed and replaced on each refresh
6. **Expiration**: Removed from storage when expired

### Token Refresh Scenarios

| Scenario | Behavior |
|----------|----------|
| Access token valid | Request proceeds normally |
| Access token expired, refresh token valid | Auto-refresh middleware issues new tokens |
| Access token expired, refresh token expired | Request fails with 401, client must re-login |
| Access token expiring soon (< 5 min) | Auto-refresh middleware proactively refreshes |
| Refresh token used twice | Second use fails (one-time use) |

---

## Security Considerations

### ✅ Implemented Security Features

1. **HTTPS Enforcement**: All endpoints require HTTPS
2. **JWT Signing**: Tokens signed with HMAC-SHA256
3. **Token Expiration**: Short-lived access tokens, longer refresh tokens
4. **One-Time Refresh Tokens**: Prevents token replay attacks
5. **Automatic Cleanup**: Expired refresh tokens removed from memory
6. **Thread-Safe Storage**: Refresh token dictionary protected with locks

### ⚠️ Current Limitations

1. **In-Memory Storage**: Refresh tokens lost on server restart
   - **Production Recommendation**: Use distributed cache (Redis) or database
2. **No User Validation**: Login endpoint accepts any username/password
   - **Production Requirement**: Integrate with user database/identity provider
3. **No Token Revocation**: Cannot revoke tokens before expiration
   - **Production Recommendation**: Implement token blacklist or use short-lived tokens

### 🔒 Production Recommendations

1. **Key Management**:
   ```bash
   # Use Azure Key Vault
   az keyvault secret set --vault-name myvault --name JwtKey --value "your-key"
   
   # Or AWS Secrets Manager
   aws secretsmanager create-secret --name jwt-key --secret-string "your-key"
   ```

2. **Refresh Token Storage**:
   ```csharp
   // Use Redis for distributed storage
   services.AddStackExchangeRedisCache(options => {
       options.Configuration = "localhost:6379";
   });
   ```

3. **User Authentication**:
   - Integrate with ASP.NET Core Identity
   - Or use external providers (Azure AD, Auth0, etc.)
   - Implement password hashing (BCrypt, Argon2)

4. **Token Revocation**:
   - Maintain a blacklist of revoked tokens
   - Check blacklist on each request
   - Store in Redis or database

5. **Rate Limiting**:
   - Already implemented via `AspNetCoreRateLimit`
   - Configure stricter limits for auth endpoints

6. **Audit Logging**:
   - Log all authentication attempts
   - Track token refresh events
   - Monitor for suspicious patterns

---

## Troubleshooting

### Issue: "Invalid or expired refresh token"

**Causes**:
- Refresh token expired (default: 7 days)
- Refresh token already used (one-time use)
- Server restarted (in-memory storage lost)

**Solution**:
- Client should re-authenticate via `/api/v1/auth/token`
- For production, use persistent storage (Redis/database)

### Issue: "Token refresh not working automatically"

**Causes**:
- Missing `X-Refresh-Token` header
- Access token not expired/expiring soon
- Middleware order incorrect

**Solution**:
- Ensure `X-Refresh-Token` header is sent with requests
- Check middleware registration order in `Program.cs`
- Verify token expiration times

### Issue: "401 Unauthorized on protected endpoints"

**Causes**:
- Missing `Authorization` header
- Invalid or expired access token
- Token signature validation failed

**Solution**:
- Verify `Authorization: Bearer <token>` header format
- Check JWT configuration (Issuer, Audience, Key)
- Ensure token hasn't expired

### Issue: "CORS errors with X-Refresh-Token header"

**Causes**:
- CORS not configured to allow `X-Refresh-Token` header

**Solution**:
- Add `X-Refresh-Token` to `AllowedHeaders` in CORS configuration
- Ensure frontend origin is in `AllowedOrigins`

---

## Additional Resources

- [JWT.io](https://jwt.io/) - JWT token decoder and validator
- [ASP.NET Core Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)

---

## Support

For issues or questions, please refer to:
- API Documentation: `/swagger`
- Health Check: `/health`
- Application Logs: Check Application Insights or console output

