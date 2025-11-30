using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Mapping;
using Application.Services;
using AutoMapper;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests;

public class ItemServiceTests : TestBase
{
    private readonly IMapper _mapper;
    private readonly ProductService _productService;
    private readonly ItemService _itemService;

    public ItemServiceTests()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile(new MappingProfile()));
        _mapper = config.CreateMapper();
        _productService = new ProductService(UnitOfWork, _mapper);
        _itemService = new ItemService(UnitOfWork, _mapper);
    }

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_Should_Return_Item_When_Exists()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        var itemDto = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(10));

        // Act
        var result = await _itemService.GetByIdAsync(product.Id, itemDto.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(itemDto.Id);
        result.ProductId.Should().Be(product.Id);
        result.Quantity.Should().Be(10);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Item_Not_Exists()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");

        // Act
        var result = await _itemService.GetByIdAsync(product.Id, 99999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_ProductId_Mismatch()
    {
        // Arrange
        var product1 = await _productService.CreateAsync(new ProductCreateDto("Product 1"), "tester");
        var product2 = await _productService.CreateAsync(new ProductCreateDto("Product 2"), "tester");
        var item = await _itemService.UpsertAsync(product1.Id, null, new ItemUpsertDto(10));

        // Act
        var result = await _itemService.GetByIdAsync(product2.Id, item.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByProductId Tests

    [Fact]
    public async Task GetByProductIdAsync_Should_Return_Item_When_Exists()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        var created = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(15));

        // Act
        var result = await _itemService.GetByProductIdAsync(product.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.ProductId.Should().Be(product.Id);
        result.Quantity.Should().Be(15);
    }

    [Fact]
    public async Task GetByProductIdAsync_Should_Return_Null_When_No_Item_Exists()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");

        // Act
        var result = await _itemService.GetByProductIdAsync(product.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Upsert Tests - Create

    [Fact]
    public async Task UpsertAsync_Should_Create_Item_When_None_Exists()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");

        // Act
        var result = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(20));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.ProductId.Should().Be(product.Id);
        result.Quantity.Should().Be(20);
    }

    [Fact]
    public async Task UpsertAsync_Should_Throw_When_Product_Not_Exists()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _itemService.UpsertAsync(99999, null, new ItemUpsertDto(10)));
    }

    #endregion

    #region Upsert Tests - Update

    [Fact]
    public async Task UpsertAsync_Should_Update_Existing_Item()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        var created = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(10));

        // Act
        var result = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(25));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(created.Id); // Same item
        result.Quantity.Should().Be(25); // Updated quantity
        
        // Verify only one item exists
        var items = await DbContext.Items.Where(i => i.ProductId == product.Id).ToListAsync();
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpsertAsync_Should_Update_When_ItemId_Matches()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        var created = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(10));

        // Act
        var result = await _itemService.UpsertAsync(product.Id, created.Id, new ItemUpsertDto(30));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(created.Id);
        result.Quantity.Should().Be(30);
    }

    [Fact]
    public async Task UpsertAsync_Should_Throw_When_ItemId_Mismatch()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        var created = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(10));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _itemService.UpsertAsync(product.Id, 99999, new ItemUpsertDto(20)));
        
        exception.Message.Should().Contain("already exists");
        exception.Message.Should().Contain(created.Id.ToString());
    }

    #endregion

    #region Uniqueness Tests

    [Fact]
    public async Task UpsertAsync_Should_Enforce_Uniqueness_Per_Product()
    {
        // Arrange
        var product = await _productService.CreateAsync(new ProductCreateDto("Test Product"), "tester");
        await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(10));

        // Act - Try to create another item for the same product
        var result = await _itemService.UpsertAsync(product.Id, null, new ItemUpsertDto(20));

        // Assert - Should update existing item, not create new one
        var items = await DbContext.Items.Where(i => i.ProductId == product.Id).ToListAsync();
        items.Should().HaveCount(1);
        items.First().Quantity.Should().Be(20);
    }

    [Fact]
    public async Task UpsertAsync_Should_Allow_Items_For_Different_Products()
    {
        // Arrange
        var product1 = await _productService.CreateAsync(new ProductCreateDto("Product 1"), "tester");
        var product2 = await _productService.CreateAsync(new ProductCreateDto("Product 2"), "tester");

        // Act
        var item1 = await _itemService.UpsertAsync(product1.Id, null, new ItemUpsertDto(10));
        var item2 = await _itemService.UpsertAsync(product2.Id, null, new ItemUpsertDto(20));

        // Assert
        item1.Should().NotBeNull();
        item2.Should().NotBeNull();
        item1.Id.Should().NotBe(item2.Id);
        item1.ProductId.Should().Be(product1.Id);
        item2.ProductId.Should().Be(product2.Id);
    }

    #endregion
}
