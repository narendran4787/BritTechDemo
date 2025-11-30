using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestId = RequestIdContext.GetCurrent(context);
        context.Response.ContentType = "application/json";
        
        object response;

        // Log the exception
        _logger.LogError(exception,
            "Unhandled exception occurred. RequestId: {RequestId}, Path: {Path}",
            requestId,
            context.Request.Path);

        // In development, include exception details
        if (_environment.IsDevelopment())
        {
            response = new
            {
                statusCode = (int)HttpStatusCode.InternalServerError,
                message = exception.Message,
                detail = exception.ToString(),
                requestId = requestId ?? "unknown",
                timestamp = DateTime.UtcNow
            };
        }
        else
        {
            response = new
            {
                statusCode = (int)HttpStatusCode.InternalServerError,
                message = "An error occurred while processing your request.",
                requestId = requestId ?? "unknown",
                timestamp = DateTime.UtcNow
            };
        }

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        //await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

