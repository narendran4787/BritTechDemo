using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Mapping;
using Application.Services;
using AutoMapper;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests;

public class ProductServiceTests : TestBase
{
    private readonly IMapper _mapper;
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new MappingProfile()));
        _mapper = config.CreateMapper();
        _service = new ProductService(UnitOfWork, _mapper);
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_Should_Create_Product_With_Correct_Properties()
    {
        // Act
        var result = await _service.CreateAsync(new ProductCreateDto("New Product"), "testuser");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ProductName.Should().Be("New Product");
        result.CreatedBy.Should().Be("testuser");
        result.CreatedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_Should_Persist_Product_To_Database()
    {
        // Act
        var created = await _service.CreateAsync(new ProductCreateDto("Persisted Product"), "creator");
        
        // Verify it can be retrieved
        var fetched = await _service.GetByIdAsync(created.Id);
        
        // Assert
        fetched.Should().NotBeNull();
        fetched!.ProductName.Should().Be("Persisted Product");
        fetched.CreatedBy.Should().Be("creator");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_Should_Return_Product_When_Exists()
    {
        // Arrange
        var created = await _service.CreateAsync(new ProductCreateDto("Test Product"), "tester");

        // Act
        var result = await _service.GetByIdAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Exists()
    {
        // Act
        var result = await _service.GetByIdAsync(99999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetPaged Tests

    [Fact]
    public async Task GetPagedAsync_Should_Return_Empty_List_When_No_Products()
    {
        // Act
        var (items, total) = await _service.GetPagedAsync(1, 10);

        // Assert
        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Return_All_Products_When_PageSize_Is_Large()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            await _service.CreateAsync(new ProductCreateDto($"Product {i}"), "tester");
        }

        // Act
        var (items, total) = await _service.GetPagedAsync(1, 10);

        // Assert
        items.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Respect_PageSize()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await _service.CreateAsync(new ProductCreateDto($"Product {i}"), "tester");
        }

        // Act
        var (items, total) = await _service.GetPagedAsync(1, 10);

        // Assert
        items.Should().HaveCount(10);
        total.Should().Be(15);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Respect_PageNumber()
    {
        // Arrange
        for (int i = 1; i <= 15; i++)
        {
            await _service.CreateAsync(new ProductCreateDto($"Product {i}"), "tester");
        }

        // Act
        var (page1, total1) = await _service.GetPagedAsync(1, 10);
        var (page2, total2) = await _service.GetPagedAsync(2, 10);

        // Assert
        page1.Should().HaveCount(10);
        page2.Should().HaveCount(5);
        total1.Should().Be(15);
        total2.Should().Be(15);
        page1.Should().NotIntersectWith(page2);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Handle_Invalid_PageNumber()
    {
        // Arrange
        await _service.CreateAsync(new ProductCreateDto("Product 1"), "tester");

        // Act
        var (items, total) = await _service.GetPagedAsync(0, 10);

        // Assert - Should default to page 1
        items.Should().HaveCount(1);
        total.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_Should_Handle_Invalid_PageSize()
    {
        // Arrange
        await _service.CreateAsync(new ProductCreateDto("Product 1"), "tester");

        // Act
        var (items, total) = await _service.GetPagedAsync(1, 0);

        // Assert - Should default to page size 10
        items.Should().HaveCount(1);
        total.Should().Be(1);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_Should_Update_Product_When_Exists()
    {
        // Arrange
        var created = await _service.CreateAsync(new ProductCreateDto("Original Name"), "creator");

        // Act
        var result = await _service.UpdateAsync(created.Id, new ProductUpdateDto("Updated Name"), "modifier");

        // Assert
        result.Should().BeTrue();
        var updated = await _service.GetByIdAsync(created.Id);
        updated.Should().NotBeNull();
        updated!.ProductName.Should().Be("Updated Name");
        updated.ModifiedBy.Should().Be("modifier");
        updated.ModifiedOn.Should().NotBeNull();
        updated.ModifiedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_False_When_Product_Not_Exists()
    {
        // Act
        var result = await _service.UpdateAsync(99999, new ProductUpdateDto("Updated Name"), "modifier");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_Should_Delete_Product_When_Exists()
    {
        // Arrange
        var created = await _service.CreateAsync(new ProductCreateDto("To Delete"), "creator");

        // Act
        var result = await _service.DeleteAsync(created.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _service.GetByIdAsync(created.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_False_When_Product_Not_Exists()
    {
        // Act
        var result = await _service.DeleteAsync(99999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_Should_Cascade_Delete_Items()
    {
        // Arrange
        var product = await _service.CreateAsync(new ProductCreateDto("Product with Items"), "creator");
        
        // Add item directly to database
        var item = new Domain.Entities.Item
        {
            ProductId = product.Id,
            Quantity = 10
        };
        DbContext.Items.Add(item);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await _service.DeleteAsync(product.Id);

        // Assert
        result.Should().BeTrue();
        var items = await DbContext.Items.Where(i => i.ProductId == product.Id).ToListAsync();
        items.Should().BeEmpty();
    }

    #endregion
}
