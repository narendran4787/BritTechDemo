#!/bin/bash

# Script to generate a development certificate for HTTPS in Docker

CERT_DIR="./certs"
CERT_FILE="$CERT_DIR/dev-cert.pfx"
CERT_PASSWORD="dev-cert-password"

# Create certs directory if it doesn't exist
mkdir -p "$CERT_DIR"

# Generate self-signed certificate
echo "Generating development certificate..."
dotnet dev-certs https --export-path "$CERT_FILE" --password "$CERT_PASSWORD" --format Pfx

if [ $? -eq 0 ]; then
    echo "Certificate generated successfully at $CERT_FILE"
    echo "Password: $CERT_PASSWORD"
    echo ""
    echo "Certificate will be mounted in Docker container at /https/dev-cert.pfx"
else
    echo "Failed to generate certificate. Trying alternative method..."
    
    # Alternative: Use openssl if dotnet dev-certs fails
    if command -v openssl &> /dev/null; then
        openssl req -x509 -newkey rsa:4096 -keyout "$CERT_DIR/dev-cert.key" -out "$CERT_DIR/dev-cert.crt" \
            -days 365 -nodes -subj "/CN=localhost"
        
        # Convert to PFX
        openssl pkcs12 -export -out "$CERT_FILE" -inkey "$CERT_DIR/dev-cert.key" \
            -in "$CERT_DIR/dev-cert.crt" -password "pass:$CERT_PASSWORD"
        
        # Clean up intermediate files
        rm "$CERT_DIR/dev-cert.key" "$CERT_DIR/dev-cert.crt"
        
        echo "Certificate generated using OpenSSL at $CERT_FILE"
    else
        echo "Error: Neither 'dotnet dev-certs' nor 'openssl' is available."
        echo "Please install .NET SDK or OpenSSL to generate the certificate."
        exit 1
    fi
fi

# Set appropriate permissions
chmod 644 "$CERT_FILE"
echo "Certificate is ready for Docker use."

