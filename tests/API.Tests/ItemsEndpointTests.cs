using System;
using System.Collections.Generic;
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

public class ItemsEndpointTests : IntegrationTestBase
{
    public ItemsEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    #region GET Tests

    [Fact]
    public async Task Get_Item_By_ProductId_Returns_Item()
    {
        // Arrange
        int productId = 0;
        int itemId = 0;
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

            var item = new Item
            {
                ProductId = productId,
                Quantity = 10
            };
            db.Items.Add(item);
            itemId = item.Id;
        });

        // Act
        var resp = await Client.GetAsync($"/api/v1/products/{productId}/items");

        // Assert
        resp.EnsureSuccessStatusCode();
        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(10);
        item.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task Get_Item_By_ProductId_Returns_404_When_No_Item_Exists()
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
        var resp = await Client.GetAsync($"/api/v1/products/{productId}/items");

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Item_By_Id_Returns_Item()
    {
        // Arrange
        int productId = 0;
        int itemId = 0;
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

            var item = new Item
            {
                ProductId = productId,
                Quantity = 15
            };
            db.Items.Add(item);
            itemId = item.Id;
        });

        // Act
        var resp = await Client.GetAsync($"/api/v1/products/{productId}/items/{itemId}");

        // Assert
        resp.EnsureSuccessStatusCode();
        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(15);
        item.Id.Should().Be(itemId);
    }

    [Fact]
    public async Task Get_Item_By_Id_Returns_404_When_Not_Found()
    {
        var resp = await Client.GetAsync("/api/v1/products/1/items/99999");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST Tests (Upsert - Create)

    [Fact]
    public async Task Create_Item_Returns_201_Created()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

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

        var dto = new ItemUpsertDto(20);

        // Act
        var resp = await Client.PostAsJsonAsync($"/api/v1/products/{productId}/items", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        resp.Headers.Location.Should().NotBeNull();

        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(20);
        item.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task Create_Item_Updates_Existing_Item_When_One_Already_Exists()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

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

            // Add existing item
            var existingItem = new Item
            {
                ProductId = productId,
                Quantity = 10
            };
            db.Items.Add(existingItem);
        });

        var dto = new ItemUpsertDto(25);

        // Act
        var resp = await Client.PostAsJsonAsync($"/api/v1/products/{productId}/items", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Created); // Still 201 as it's a POST

        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(25); // Updated quantity

        // Verify only one item exists
        var getResp = await Client.GetAsync($"/api/v1/products/{productId}/items");
        var retrieved = await getResp.Content.ReadFromJsonAsync<ItemDto>();
        retrieved!.Quantity.Should().Be(25);
    }

    [Fact]
    public async Task Create_Item_Returns_404_When_Product_Not_Found()
    {
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        var dto = new ItemUpsertDto(20);
        var resp = await Client.PostAsJsonAsync("/api/v1/products/99999/items", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Item_Returns_401_When_Unauthorized()
    {
        ClearAuthHeader();
        var dto = new ItemUpsertDto(20);
        var resp = await Client.PostAsJsonAsync("/api/v1/products/1/items", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Item_Returns_400_When_Validation_Fails()
    {
        var token = GenerateJwtToken();
        SetAuthHeader(token);

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

        var dto = new ItemUpsertDto(-1); // Invalid quantity

        var resp = await Client.PostAsJsonAsync($"/api/v1/products/{productId}/items", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT Tests (Upsert - Update)

    [Fact]
    public async Task Update_Item_Returns_200_OK()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        int productId = 0;
        int itemId = 0;
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

            var item = new Item
            {
                ProductId = productId,
                Quantity = 10
            };
            db.Items.Add(item);
            itemId = item.Id;
        });

        var dto = new ItemUpsertDto(30);

        // Act
        var resp = await Client.PutAsJsonAsync($"/api/v1/products/{productId}/items/{itemId}", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(30);
    }

    [Fact]
    public async Task Update_Item_Creates_New_Item_If_None_Exists()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

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

        var dto = new ItemUpsertDto(40);

        // Act
        var resp = await Client.PutAsJsonAsync($"/api/v1/products/{productId}/items/1", dto);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = await resp.Content.ReadFromJsonAsync<ItemDto>();
        item.Should().NotBeNull();
        item!.Quantity.Should().Be(40);
    }

    [Fact]
    public async Task Update_Item_Returns_400_When_ItemId_Mismatch()
    {
        // Arrange
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        int productId = 0;
        int itemId = 0;
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

            var item = new Item
            {
                ProductId = productId,
                Quantity = 10
            };
            db.Items.Add(item);
            itemId = item.Id;
        });

        var dto = new ItemUpsertDto(30);

        // Act - Try to update with wrong itemId
        var resp = await Client.PutAsJsonAsync($"/api/v1/products/{productId}/items/99999", dto);

        // Assert - Should return 400 or 404 depending on implementation
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Item_Returns_404_When_Product_Not_Found()
    {
        var token = GenerateJwtToken();
        SetAuthHeader(token);

        var dto = new ItemUpsertDto(30);
        var resp = await Client.PutAsJsonAsync("/api/v1/products/99999/items/1", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Item_Returns_401_When_Unauthorized()
    {
        ClearAuthHeader();
        var dto = new ItemUpsertDto(30);
        var resp = await Client.PutAsJsonAsync("/api/v1/products/1/items/1", dto);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}

