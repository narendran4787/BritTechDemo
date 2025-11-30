using Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

/// <summary>
/// Extension methods for logging with RequestId context
/// </summary>
public static class RequestIdLoggingExtensions
{
    /// <summary>
    /// Logs a message with RequestId from HttpContext or AsyncLocal
    /// </summary>
    public static void LogWithRequestId(
        this ILogger logger,
        LogLevel logLevel,
        string message,
        params object[] args)
    {
        var requestId = RequestIdContext.Current ?? "unknown";
        var enrichedMessage = $"[RequestId: {requestId}] {message}";
        logger.Log(logLevel, enrichedMessage, args);
    }

    /// <summary>
    /// Logs an information message with RequestId
    /// </summary>
    public static void LogInformationWithRequestId(
        this ILogger logger,
        string message,
        params object[] args)
    {
        logger.LogWithRequestId(LogLevel.Information, message, args);
    }

    /// <summary>
    /// Logs a warning message with RequestId
    /// </summary>
    public static void LogWarningWithRequestId(
        this ILogger logger,
        string message,
        params object[] args)
    {
        logger.LogWithRequestId(LogLevel.Warning, message, args);
    }

    /// <summary>
    /// Logs an error message with RequestId
    /// </summary>
    public static void LogErrorWithRequestId(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        var requestId = RequestIdContext.Current ?? "unknown";
        var enrichedMessage = $"[RequestId: {requestId}] {message}";
        logger.LogError(exception, enrichedMessage, args);
    }

    /// <summary>
    /// Logs an error message with RequestId
    /// </summary>
    public static void LogErrorWithRequestId(
        this ILogger logger,
        string message,
        params object[] args)
    {
        logger.LogWithRequestId(LogLevel.Error, message, args);
    }

    /// <summary>
    /// Logs a debug message with RequestId
    /// </summary>
    public static void LogDebugWithRequestId(
        this ILogger logger,
        string message,
        params object[] args)
    {
        logger.LogWithRequestId(LogLevel.Debug, message, args);
    }

    /// <summary>
    /// Creates a scoped logger with RequestId in the scope
    /// </summary>
    public static IDisposable? BeginScopeWithRequestId(this ILogger logger, HttpContext? context = null)
    {
        var requestId = RequestIdContext.GetCurrent(context) ?? "unknown";
        return logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = requestId });
    }
}

