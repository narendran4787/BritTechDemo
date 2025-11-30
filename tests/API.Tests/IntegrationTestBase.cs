using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Solution.API;
using Xunit;

namespace API.Tests;

public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    private readonly string _databaseName;

    protected IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        // Use a unique database name per test class instance to ensure isolation
        _databaseName = $"TestDb_{Guid.NewGuid()}";
        
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove all existing DbContext-related registrations
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext) ||
                               d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                               (d.ServiceType.IsGenericType && 
                                d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing (shared across all scopes in this test class)
                // The database name is consistent per test class instance, so all scopes share the same data
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                    options.EnableSensitiveDataLogging(); // Helpful for debugging tests
                });
            });
            
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Disable HTTPS requirement for tests
                var testConfig = new Dictionary<string, string>
                {
                    ["ASPNETCORE_URLS"] = "https://localhost"
                };
                config.AddInMemoryCollection(testConfig);
            });
        });

        // Create client with HTTPS support
        // WebApplicationFactory automatically handles certificate validation in test mode
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = true
        });
    }

    protected string GenerateJwtToken(string userId = "test-user", string username = "testuser", string[] roles = null)
    {
        using var scope = Factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (accessToken, _) = tokenService.CreateTokens(userId, username, roles);
        return accessToken;
    }

    protected void SetAuthHeader(string token)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    protected void ClearAuthHeader()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    protected async Task SeedDatabaseAsync(Action<ApplicationDbContext> seedAction)
    {
        // Use the same service provider that the API uses
        // This ensures we're using the same in-memory database instance
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        seedAction(db);
        
        // Save changes - this persists to the in-memory database
        await db.SaveChangesAsync();
        
        // Dispose the scope to ensure the DbContext is properly disposed
        // The in-memory database data persists even after disposal
    }

    public void Dispose()
    {
        Client?.Dispose();
    }
}

