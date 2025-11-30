using System.Text;
using Application.Interfaces;
using Application.Mapping;
using Application.Services;
using Application.Validators;
using Asp.Versioning;
using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Middleware;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Application Insights
var applicationInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = applicationInsightsConnectionString;
        options.EnableAdaptiveSampling = false; // Log all requests
    });
}

// Serilog with Application Insights sink
builder.Host.UseSerilog((context, lc) =>
{
    lc.ReadFrom.Configuration(context.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", "ProductsAPI");

    // Add Application Insights sink if connection string is provided
    if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
    {
        lc.WriteTo.ApplicationInsights(
            TelemetryConfiguration.CreateDefault(),
            TelemetryConverter.Traces);
    }

    lc.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{RequestId}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});

var services = builder.Services;
var config = builder.Configuration;

// Configure Kestrel for HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTPS endpoint configuration
    options.ListenAnyIP(8443, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        
        // Use certificate from environment variables if available
        var certPath = config["ASPNETCORE_Kestrel__Certificates__Default__Path"];
        var certPassword = config["ASPNETCORE_Kestrel__Certificates__Default__Password"];
        
        if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath) && !string.IsNullOrEmpty(certPassword))
        {
            listenOptions.UseHttps(certPath, certPassword);
        }
        else
        {
            // Fallback: use HTTPS without certificate (for development)
            listenOptions.UseHttps();
        }
    });
});

services.AddControllers();

services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Products API", 
        Version = "v1",
        Description = "Products API with JWT authentication and automatic token refresh",
        Contact = new OpenApiContact
        {
            Name = "API Support"
        }
    });
    
    // JWT Bearer Authentication
    var securitySchema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
    };
    c.AddSecurityDefinition("Bearer", securitySchema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new List<string>()
        }
    });
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// API versioning
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion =  new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Database Context
var useSql = config.GetValue<bool>("UseSqlServer", false);
if (useSql)
{
    services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(
            config.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null));
    });
}
else
{
    services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ProductsDb"));
}

// UnitOfWork
services.AddScoped<IUnitOfWork, UnitOfWork<ApplicationDbContext>>();

// AutoMapper
services.AddAutoMapper(typeof(MappingProfile).Assembly);

// FluentValidation
services.AddFluentValidationAutoValidation();
services.AddValidatorsFromAssemblyContaining<ProductCreateValidator>();

// JWT
services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
services.AddScoped<IProductService, ProductService>();
services.AddScoped<IItemService, ItemService>();
services.AddSingleton<ITokenService, TokenService>();
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });
services.AddAuthorization();

// Rate Limiting
var rateLimitConfig = config.GetSection("RateLimiting");
if (rateLimitConfig.GetValue<bool>("EnableRateLimiting", true))
{
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(rateLimitConfig);
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
}

// Health Checks
services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database")
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" });

// CORS
var corsConfig = config.GetSection("Cors");
var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowedMethods = corsConfig.GetSection("AllowedMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE" };
var allowedHeaders = corsConfig.GetSection("AllowedHeaders").Get<string[]>() ?? new[] { "Content-Type", "Authorization" };
var allowCredentials = corsConfig.GetValue<bool>("AllowCredentials", true);
var maxAge = corsConfig.GetValue<int>("MaxAge", 3600);

services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
            if (allowCredentials)
            {
                policy.AllowCredentials();
            }
        }
        else
        {
            policy.AllowAnyOrigin();
        }
        
        policy.WithMethods(allowedMethods)
              .WithHeaders(allowedHeaders)
              .SetPreflightMaxAge(TimeSpan.FromSeconds(maxAge));
    });
});

// Compression
services.AddResponseCompression();

var app = builder.Build();

// Ensure database is created (only for SQL Server)
if (config.GetValue<bool>("UseSqlServer", false))
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Database.EnsureCreated();
            Console.WriteLine("[INFO] Database ensured/created successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to ensure database creation: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            // Don't fail startup - allow retry on first request
        }
    }
}

// Exception handling middleware (must be first to catch all exceptions)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Security headers middleware
app.UseMiddleware<SecurityHeadersMiddleware>();

// RequestId middleware (must be early to track all requests)
app.UseMiddleware<RequestIdMiddleware>();

// Rate limiting (before other middleware to reject early)
if (config.GetValue<bool>("RateLimiting:EnableRateLimiting", true))
{
    app.UseIpRateLimiting();
}

// Application Insights logging middleware (logs requests/responses with PII filtering)
app.UseMiddleware<ApplicationInsightsLoggingMiddleware>();

// Serilog request logging (enhanced with RequestId)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var requestId = RequestIdContext.GetCurrent(httpContext);
        diagnosticContext.Set("RequestId", requestId ?? "unknown");
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

// Health checks endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self")
});

// Swagger (development only)
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Products API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
        c.EnableValidator();
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    });
// }

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseCors("Default");

// Automatic token refresh middleware (before authentication)
app.UseMiddleware<AutoTokenRefreshMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
namespace Solution.API
{
    public partial class Program { }
}
