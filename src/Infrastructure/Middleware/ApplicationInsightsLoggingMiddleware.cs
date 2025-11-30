using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware;

/// <summary>
/// Middleware that logs all requests and responses to Application Insights, excluding PII
/// </summary>
public class ApplicationInsightsLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<ApplicationInsightsLoggingMiddleware> _logger;
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "X-Api-Key",
        "X-Auth-Token"
    };

    private static readonly HashSet<string> SensitiveBodyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwordhash",
        "oldpassword",
        "newpassword",
        "token",
        "accesstoken",
        "refreshtoken",
        "apikey",
        "secret",
        "ssn",
        "socialsecuritynumber",
        "creditcard",
        "cardnumber",
        "cvv",
        "email",
        "phone",
        "phonenumber"
    };

    public ApplicationInsightsLoggingMiddleware(
        RequestDelegate next,
        TelemetryClient telemetryClient,
        ILogger<ApplicationInsightsLoggingMiddleware> logger)
    {
        _next = next;
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = RequestIdContext.GetCurrent(context);
        var stopwatch = Stopwatch.StartNew();

        // Skip logging for health checks and swagger
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Log request
        await LogRequestAsync(context, requestId);

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            await LogResponseAsync(context, requestId, stopwatch.ElapsedMilliseconds);

            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequestAsync(HttpContext context, string? requestId)
    {
        try
        {
            var request = context.Request;
            var properties = new Dictionary<string, string>
            {
                ["RequestId"] = requestId ?? "unknown",
                ["Method"] = request.Method,
                ["Path"] = request.Path.Value ?? string.Empty,
                ["QueryString"] = request.QueryString.Value ?? string.Empty,
                ["Scheme"] = request.Scheme,
                ["Host"] = request.Host.Value ?? string.Empty,
                ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };

            // Log headers (excluding sensitive ones)
            var headers = FilterSensitiveHeaders(request.Headers);
            foreach (var header in headers)
            {
                properties[$"RequestHeader_{header.Key}"] = header.Value.ToString();
            }

            // Log request body if present (excluding PII)
            if (request.ContentLength > 0 && request.ContentType?.Contains("application/json") == true)
            {
                request.EnableBuffering();
                var body = await ReadRequestBodyAsync(request);
                var sanitizedBody = SanitizeJsonBody(body);
                if (!string.IsNullOrEmpty(sanitizedBody))
                {
                    properties["RequestBody"] = sanitizedBody;
                }
            }

            _telemetryClient.TrackTrace(
                $"HTTP Request: {request.Method} {request.Path}",
                SeverityLevel.Information,
                properties);

            _logger.LogInformation(
                "HTTP Request: {Method} {Path} | RequestId: {RequestId}",
                request.Method,
                request.Path,
                requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging request");
        }
    }

    private async Task LogResponseAsync(HttpContext context, string? requestId, long elapsedMilliseconds)
    {
        try
        {
            var response = context.Response;
            var properties = new Dictionary<string, string>
            {
                ["RequestId"] = requestId ?? "unknown",
                ["StatusCode"] = response.StatusCode.ToString(),
                ["ElapsedMilliseconds"] = elapsedMilliseconds.ToString()
            };

            // Log response headers (excluding sensitive ones)
            var headers = FilterSensitiveHeaders(response.Headers);
            foreach (var header in headers)
            {
                properties[$"ResponseHeader_{header.Key}"] = header.Value.ToString();
            }

            // Log response body if present (excluding PII)
            if (response.Body.CanSeek && response.Body.Length > 0)
            {
                response.Body.Seek(0, SeekOrigin.Begin);
                var body = await new StreamReader(response.Body).ReadToEndAsync();
                response.Body.Seek(0, SeekOrigin.Begin);
                
                var sanitizedBody = SanitizeJsonBody(body);
                if (!string.IsNullOrEmpty(sanitizedBody))
                {
                    properties["ResponseBody"] = sanitizedBody;
                }
            }

            var severity = response.StatusCode >= 500
                ? SeverityLevel.Error
                : response.StatusCode >= 400
                    ? SeverityLevel.Warning
                    : SeverityLevel.Information;

            _telemetryClient.TrackTrace(
                $"HTTP Response: {response.StatusCode} | {elapsedMilliseconds}ms",
                severity,
                properties);

            var dependencyTelemetry = new DependencyTelemetry
            {
                Type = "HTTP",
                Name = $"{context.Request.Method} {context.Request.Path}",
                Timestamp = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromMilliseconds(elapsedMilliseconds),
                Success = response.StatusCode < 400
            };
            dependencyTelemetry.Properties["RequestId"] = requestId ?? "unknown";
            _telemetryClient.TrackDependency(dependencyTelemetry);

            _logger.LogInformation(
                "HTTP Response: {StatusCode} | {ElapsedMs}ms | RequestId: {RequestId}",
                response.StatusCode,
                elapsedMilliseconds,
                requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging response");
        }
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> FilterSensitiveHeaders(
        IHeaderDictionary headers)
    {
        return headers
            .Where(h => !SensitiveHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value);
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static string SanitizeJsonBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var sanitized = SanitizeJsonElement(doc.RootElement);
            return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            // If not valid JSON, return as-is but check for sensitive patterns
            return body.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                   body.Contains("token", StringComparison.OrdinalIgnoreCase)
                ? "[REDACTED - Contains sensitive data]"
                : body;
        }
    }

    private static object SanitizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SanitizeObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(SanitizeJsonElement).ToArray(),
            JsonValueKind.String => SanitizeStringValue(element.GetString() ?? string.Empty),
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object> SanitizeObject(JsonElement obj)
    {
        var result = new Dictionary<string, object>();
        foreach (var property in obj.EnumerateObject())
        {
            var key = property.Name;
            var value = property.Value;

            if (SensitiveBodyFields.Contains(key))
            {
                result[key] = "[REDACTED]";
            }
            else
            {
                result[key] = SanitizeJsonElement(value);
            }
        }
        return result;
    }

    private static string SanitizeStringValue(string value)
    {
        // Check if value looks like sensitive data (email, phone, etc.)
        if (value.Contains("@") && value.Contains("."))
            return "[REDACTED - Email]";
        
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{3}-\d{2}-\d{4}$") ||
            System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{10}$"))
            return "[REDACTED - SSN/Phone]";

        return value;
    }
}

