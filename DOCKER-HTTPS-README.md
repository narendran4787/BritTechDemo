# Docker HTTPS Configuration

This project is configured to use **HTTPS only** in Docker containers.

## Port Configuration

- **API Service**: Port `8443` (HTTPS only)
- **SQL Server**: Port `1433` (unchanged)

## Certificate Setup

Before running Docker Compose, you need to generate a development certificate:

### Option 1: Using .NET SDK (Recommended)

```bash
./generate-dev-cert.sh
```

This script will:
- Generate a self-signed certificate using `dotnet dev-certs`
- Save it to `./certs/dev-cert.pfx`
- Set the password to `dev-cert-password`

### Option 2: Manual Generation

If the script doesn't work, you can generate the certificate manually:

```bash
# Create certs directory
mkdir -p certs

# Generate certificate using .NET
dotnet dev-certs https --export-path ./certs/dev-cert.pfx --password dev-cert-password --format Pfx
```

### Option 3: Using OpenSSL

```bash
mkdir -p certs

# Generate certificate
openssl req -x509 -newkey rsa:4096 -keyout certs/dev-cert.key -out certs/dev-cert.crt \
    -days 365 -nodes -subj "/CN=localhost"

# Convert to PFX
openssl pkcs12 -export -out certs/dev-cert.pfx -inkey certs/dev-cert.key \
    -in certs/dev-cert.crt -password pass:dev-cert-password

# Clean up
rm certs/dev-cert.key certs/dev-cert.crt
```

## Running Docker Compose

After generating the certificate:

```bash
# Build and start services
docker compose up --build

# Or run in detached mode
docker compose up -d --build
```

## Accessing the API

Once running, access the API via HTTPS:

- **API**: `https://localhost:8443`
- **Swagger UI**: `https://localhost:8443/swagger`
- **Health Check**: `https://localhost:8443/health`

## Certificate Trust (Development)

Since this is a self-signed certificate, your browser will show a security warning. To trust it:

### macOS
```bash
# Import certificate to keychain
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain certs/dev-cert.pfx
```

### Linux
```bash
# Copy certificate to system trust store
sudo cp certs/dev-cert.crt /usr/local/share/ca-certificates/dev-cert.crt
sudo update-ca-certificates
```

### Windows
1. Double-click `certs/dev-cert.pfx`
2. Follow the certificate import wizard
3. Select "Trusted Root Certification Authorities" as the store

## Production Considerations

For production:
1. Use a proper SSL certificate from a Certificate Authority (CA)
2. Store the certificate securely (e.g., Azure Key Vault, AWS Secrets Manager)
3. Use environment variables or secure configuration for certificate paths
4. Consider using port 443 instead of 8443
5. Set up proper certificate rotation

## Troubleshooting

### Certificate Not Found
If you see errors about the certificate not being found:
1. Ensure `certs/dev-cert.pfx` exists
2. Check file permissions (should be readable)
3. Verify the certificate password matches `dev-cert-password`

### Connection Refused
- Ensure the container is running: `docker compose ps`
- Check logs: `docker compose logs api`
- Verify port 8443 is not already in use

### Certificate Validation Errors
- For development, you may need to accept the self-signed certificate
- Use `curl -k` or configure your client to skip certificate validation (development only)

