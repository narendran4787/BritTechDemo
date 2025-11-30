// Custom Swagger UI JavaScript
// This file provides additional information about custom headers and response headers

window.onload = function() {
    // Add info about custom headers
    const headerInfo = `
        <div style="margin: 20px; padding: 15px; background-color: #f0f8ff; border-left: 4px solid #0066cc; border-radius: 4px;">
            <h3 style="margin-top: 0; color: #0066cc;">📋 Custom Headers Information</h3>
            <ul style="margin-bottom: 0;">
                <li><strong>X-Refresh-Token</strong>: Optional. Used for automatic token refresh when access token expires.</li>
                <li><strong>X-Request-Id</strong>: Optional. For request tracking and correlation. Auto-generated if not provided.</li>
                <li><strong>Authorization</strong>: Required for protected endpoints. Format: "Bearer {access_token}"</li>
            </ul>
            <h4 style="color: #0066cc; margin-top: 15px;">Response Headers</h4>
            <ul style="margin-bottom: 0;">
                <li><strong>X-Request-Id</strong>: Request ID for tracking (always returned)</li>
                <li><strong>X-New-Access-Token</strong>: New access token (when auto-refresh occurs)</li>
                <li><strong>X-New-Refresh-Token</strong>: New refresh token (when auto-refresh occurs)</li>
                <li><strong>X-Token-Refreshed</strong>: "true" when token was automatically refreshed</li>
                <li><strong>X-Total-Count</strong>: Total count for paginated responses (GET /products)</li>
            </ul>
        </div>
    `;
    
    // Try to inject the info into Swagger UI
    setTimeout(function() {
        const swaggerContainer = document.querySelector('.swagger-ui');
        if (swaggerContainer) {
            const infoDiv = document.createElement('div');
            infoDiv.innerHTML = headerInfo;
            swaggerContainer.insertBefore(infoDiv, swaggerContainer.firstChild);
        }
    }, 500);
};

