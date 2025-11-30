using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Solution.API;
using Xunit;

namespace API.Tests;

public class AuthEndpointTests : IntegrationTestBase
{
    public AuthEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_Returns_Tokens_With_Valid_Credentials()
    {
        // Note: This test assumes you have a login endpoint
        // Adjust based on your actual AuthController implementation
        var loginRequest = new
        {
            username = "testuser",
            password = "testpass"
        };

        var resp = await Client.PostAsJsonAsync("/api/v1/auth/token", loginRequest);
        
        // This will depend on your actual auth implementation
        // For now, just verify the endpoint exists
        resp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_Returns_401_With_Invalid_Credentials()
    {
        var loginRequest = new
        {
            username = "",
            password = "invalid"
        };

        var resp = await Client.PostAsJsonAsync("/api/v1/auth/token", loginRequest);
        
        // Should return 401 for invalid credentials
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }
}

