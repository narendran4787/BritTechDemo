using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Application.DTOs;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Solution.API;
using Xunit;

namespace API.Tests;

public class ProductsEndpointTests : IntegrationTestBase
{
    public ProductsEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    #region GET Tests

    [Fact]
    public async Task Get_Products_Returns_Empty_List_Initially()
    {
        var resp = await Client.GetAsync("/api/v1/products");
        resp.EnsureSuccessStatusCode();
        var items = await resp.Content.ReadFromJsonAsync<List<ProductDto>>();
        items.Should().NotBeNull();
        items!.Count.Should().Be(0);
    }

    [Fact]
    public async Task Get_Products_Returns_Products_With_Pagination()
    {
        // Arrange
        await SeedDatabaseAsync(db =>
        {
            for (int i = 1; i <= 15; i++)
            {
                db.Products.Add(new Product
                {
                    ProductName = $"Product {i}",
                    CreatedBy = "test",
                    CreatedOn = DateTime.UtcNow
                });
            }
        });

        // Act
        var resp = await Client.GetAsync("/api/v1/products?pageNumber=1&pageSize=10");
        resp.EnsureSuccessStatusCode();

        // Assert
        var items = await resp.Content.ReadFromJsonAsync<List<ProductDto>>();
        items.Should().NotBeNull();
        items!.Count.Should().Be(10);
        
        var totalCount = resp.Headers.GetValues("X-Total-Count").FirstOrDefault();
        totalCount.Should().Be("15");
    }

    [Fact]
    public async Task Get_Product_By_Id_Returns_Product()
    {
        // Arrange
        int productId = 0;
        await SeedDatabaseAsync(db =>
        {
            var product = new Product
            {
                ProductName = "Test Product",
                CreatedBy = "test",
                CreatedOn = DateTime.UtcNow
            };
            db.Products.Add(product);
            productId = product.Id;
        });

        // Act
        var resp = await Client.GetAsync($"/api/v1/products/{productId}");

        // Assert
        resp.EnsureSuccessStatusCode();
        var product = await resp.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();
        product!.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task Get_Product_By_Id_Returns_404_When_Not_Found()
    {
        var resp = await Client.GetAsync("/api/v1/products/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task Create_Product_Returns_201_With_Location_Header()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        var dto = new ProductCreateDto("New Product");

        // Act
        var resp = await Client.PostAsJsonAsync("/api/v1/products", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        resp.Headers.Location.Should().NotBeNull();
        
        var product = await resp.Content.ReadFromJsonAsync<ProductDto>();
        product.Should().NotBeNull();
        product!.ProductName.Should().Be("New Product");
        product.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Create_Product_Returns_401_When_Unauthorized()
    {
        ClearAuthHeader();
        var dto = new ProductCreateDto("New Product");
        var resp = await Client.PostAsJsonAsync("/api/v1/products", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Product_Returns_400_When_Validation_Fails()
    {
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        var dto = new ProductCreateDto(""); // Empty name should fail validation

        var resp = await Client.PostAsJsonAsync("/api/v1/products", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT Tests

    [Fact]
    public async Task Update_Product_Returns_204_No_Content()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        int productId = 0;
        await SeedDatabaseAsync(db =>
        {
            var product = new Product
            {
                ProductName = "Original Name",
                CreatedBy = "test",
                CreatedOn = DateTime.UtcNow
            };
            db.Products.Add(product);
            productId = product.Id;
        });

        var dto = new ProductUpdateDto("Updated Name");

        // Act
        var resp = await Client.PutAsJsonAsync($"/api/v1/products/{productId}", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify update
        var getResp = await Client.GetAsync($"/api/v1/products/{productId}");
        var updated = await getResp.Content.ReadFromJsonAsync<ProductDto>();
        updated!.ProductName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Update_Product_Returns_404_When_Not_Found()
    {
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        var dto = new ProductUpdateDto("Updated Name");
        var resp = await Client.PutAsJsonAsync("/api/v1/products/99999", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Product_Returns_401_When_Unauthorized()
    {
        ClearAuthHeader();
        var dto = new ProductUpdateDto("Updated Name");
        var resp = await Client.PutAsJsonAsync("/api/v1/products/1", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DELETE Tests

    [Fact]
    public async Task Delete_Product_Returns_204_No_Content()
    {
        // Arrange
        var token = GenerateJwtToken("test-user", "testuser", new[] { "Admin" });
        SetAuthHeader(token);

        int productId = 0;
        await SeedDatabaseAsync(db =>
        {
            var product = new Product
            {
                ProductName = "To Delete",
                CreatedBy = "test",
                CreatedOn = DateTime.UtcNow
            };
            db.Products.Add(product);
            productId = product.Id;
            
        });

        // Act
        var resp = await Client.DeleteAsync($"/api/v1/products/{productId}");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        var getResp = await Client.GetAsync($"/api/v1/products/{productId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Product_Returns_403_When_Not_Admin()
    {
        var token = GenerateJwtToken("test-user", "testuser", Array.Empty<string>());
        SetAuthHeader(token);

        var resp = await Client.DeleteAsync("/api/v1/products/1");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Product_Returns_404_When_Not_Found()
    {
        var token = GenerateJwtToken("test-user", "testuser", new[] { "Admin" });
        SetAuthHeader(token);

        var resp = await Client.DeleteAsync("/api/v1/products/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
