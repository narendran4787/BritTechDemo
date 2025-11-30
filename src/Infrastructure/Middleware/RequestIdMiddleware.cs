using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Middleware;

/// <summary>
/// Middleware that generates and tracks a unique RequestId throughout the request lifecycle
/// </summary>
public class RequestIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string RequestIdHeaderName = "X-Request-Id";
    public const string RequestIdKey = "RequestId";

    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate RequestId
        var requestId = context.Request.Headers[RequestIdHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        // Store in HttpContext for access throughout the request
        context.Items[RequestIdKey] = requestId;
        
        // Add to Activity for distributed tracing
        Activity.Current?.SetTag(RequestIdKey, requestId);
        
        // Add to response headers
        context.Response.Headers[RequestIdHeaderName] = requestId;

        // Store in AsyncLocal for access outside HttpContext (e.g., in background services)
        RequestIdContext.Current = requestId;

        try
        {
            await _next(context);
        }
        finally
        {
            // Clear AsyncLocal after request completes
            RequestIdContext.Current = null;
        }
    }
}

/// <summary>
/// Provides access to RequestId outside of HttpContext using AsyncLocal
/// </summary>
public static class RequestIdContext
{
    private static readonly AsyncLocal<string?> _requestId = new();

    public static string? Current
    {
        get => _requestId.Value;
        set => _requestId.Value = value;
    }

    /// <summary>
    /// Gets the current RequestId from HttpContext or AsyncLocal
    /// </summary>
    public static string? GetCurrent(HttpContext? context = null)
    {
        if (context != null && context.Items.TryGetValue(RequestIdMiddleware.RequestIdKey, out var requestId))
        {
            return requestId?.ToString();
        }
        return Current;
    }
}

