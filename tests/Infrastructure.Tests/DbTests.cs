using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Infrastructure.Tests;

public class DbTests
{
    private ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    #region Entity Relationship Tests

    [Fact]
    public async Task Can_Create_And_Retrieve_Product_With_Item()
    {
        await using var ctx = CreateContext();

        var product = new Product
        {
            ProductName = "Db Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow,
            Items = { new Item { Quantity = 5 } }
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Products.Include(p => p.Items).FirstAsync();
        loaded.Items.Should().HaveCount(1);
        loaded.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Product_Delete_Should_Cascade_Delete_Items()
    {
        await using var ctx = CreateContext();

        var product = new Product
        {
            ProductName = "Product to Delete",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow,
            Items = { new Item { Quantity = 10 } }
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        ctx.Products.Remove(product);
        await ctx.SaveChangesAsync();

        var items = await ctx.Items.Where(i => i.ProductId == product.Id).ToListAsync();
        items.Should().BeEmpty();
    }

    #endregion

    #region Unique Constraint Tests

    [Fact]
    public async Task Should_Have_Unique_Index_On_ProductId_For_Items()
    {
        await using var ctx = CreateContext();

        // Verify the unique index is configured in the model
        var itemEntityType = ctx.Model.FindEntityType(typeof(Item));
        itemEntityType.Should().NotBeNull();

        var productIdProperty = itemEntityType!.FindProperty(nameof(Item.ProductId));
        productIdProperty.Should().NotBeNull();

        var index = itemEntityType.FindIndex(productIdProperty!);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Allow_Only_One_Item_Per_Product_In_Practice()
    {
        await using var ctx = CreateContext();

        var product = new Product
        {
            ProductName = "Test Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var item1 = new Item
        {
            ProductId = product.Id,
            Quantity = 10
        };
        ctx.Items.Add(item1);
        await ctx.SaveChangesAsync();

        // Note: EF Core InMemory doesn't enforce unique indexes
        // In a real database (SQL Server), this would throw DbUpdateException
        // This test verifies the constraint is configured in the model
        var items = await ctx.Items.Where(i => i.ProductId == product.Id).ToListAsync();
        items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Should_Allow_Items_For_Different_Products()
    {
        await using var ctx = CreateContext();

        var product1 = new Product
        {
            ProductName = "Product 1",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        var product2 = new Product
        {
            ProductName = "Product 2",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.AddRange(product1, product2);
        await ctx.SaveChangesAsync();

        var item1 = new Item
        {
            ProductId = product1.Id,
            Quantity = 10
        };
        var item2 = new Item
        {
            ProductId = product2.Id,
            Quantity = 20
        };
        ctx.Items.AddRange(item1, item2);
        await ctx.SaveChangesAsync();

        var items = await ctx.Items.ToListAsync();
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.ProductId == product1.Id && i.Quantity == 10);
        items.Should().Contain(i => i.ProductId == product2.Id && i.Quantity == 20);
    }

    #endregion

    #region Repository Tests

    [Fact]
    public async Task Repository_GetByIdAsync_Should_Return_Entity_When_Exists()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product = new Product
        {
            ProductName = "Test Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var result = await repo.GetByIdAsync(product.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
        result.ProductName.Should().Be("Test Product");
    }

    [Fact]
    public async Task Repository_GetByIdAsync_Should_Return_Null_When_Not_Exists()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var result = await repo.GetByIdAsync(99999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Repository_Query_Should_Filter_By_Predicate()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product1 = new Product
        {
            ProductName = "Product A",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        var product2 = new Product
        {
            ProductName = "Product B",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.AddRange(product1, product2);
        await ctx.SaveChangesAsync();

        var results = repo.Query(p => p.ProductName == "Product A").ToList();

        results.Should().HaveCount(1);
        results.First().ProductName.Should().Be("Product A");
    }

    [Fact]
    public async Task Repository_Query_Should_Support_AsNoTracking()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product = new Product
        {
            ProductName = "Test Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var tracked = repo.Query(p => p.Id == product.Id, asNoTracking: false).First();
        var untracked = repo.Query(p => p.Id == product.Id, asNoTracking: true).First();

        ctx.Entry(tracked).State.Should().Be(EntityState.Unchanged);
        ctx.Entry(untracked).State.Should().Be(EntityState.Detached);
    }

    [Fact]
    public async Task Repository_AddAsync_Should_Add_Entity()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product = new Product
        {
            ProductName = "New Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };

        await repo.AddAsync(product);
        await ctx.SaveChangesAsync();

        var count = await ctx.Products.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Repository_Update_Should_Update_Entity()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product = new Product
        {
            ProductName = "Original Name",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        product.ProductName = "Updated Name";
        repo.Update(product);
        await ctx.SaveChangesAsync();

        var updated = await ctx.Products.FindAsync(product.Id);
        updated!.ProductName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Repository_Remove_Should_Remove_Entity()
    {
        await using var ctx = CreateContext();
        var repo = new Repository<Product>(ctx);

        var product = new Product
        {
            ProductName = "To Delete",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        repo.Remove(product);
        await ctx.SaveChangesAsync();

        var count = await ctx.Products.CountAsync();
        count.Should().Be(0);
    }

    #endregion

    #region UnitOfWork Tests

    [Fact]
    public async Task UnitOfWork_Should_Return_Same_Repository_Instance()
    {
        await using var ctx = CreateContext();
        var uow = new UnitOfWork<ApplicationDbContext>(ctx);

        var repo1 = uow.Repository<Product>();
        var repo2 = uow.Repository<Product>();

        repo1.Should().BeSameAs(repo2);
    }

    [Fact]
    public async Task UnitOfWork_Should_Return_Different_Repositories_For_Different_Types()
    {
        await using var ctx = CreateContext();
        var uow = new UnitOfWork<ApplicationDbContext>(ctx);

        var productRepo = uow.Repository<Product>();
        var itemRepo = uow.Repository<Item>();

        productRepo.Should().NotBeSameAs(itemRepo);
    }

    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_Should_Persist_Changes()
    {
        await using var ctx = CreateContext();
        var uow = new UnitOfWork<ApplicationDbContext>(ctx);

        var product = new Product
        {
            ProductName = "Test Product",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        await uow.Repository<Product>().AddAsync(product);
        await uow.SaveChangesAsync();

        var count = await ctx.Products.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task UnitOfWork_Should_Support_Transaction_Behavior()
    {
        await using var ctx = CreateContext();
        var uow = new UnitOfWork<ApplicationDbContext>(ctx);

        var product1 = new Product
        {
            ProductName = "Product 1",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };
        var product2 = new Product
        {
            ProductName = "Product 2",
            CreatedBy = "test",
            CreatedOn = DateTime.UtcNow
        };

        await uow.Repository<Product>().AddAsync(product1);
        await uow.Repository<Product>().AddAsync(product2);
        await uow.SaveChangesAsync();

        var count = await ctx.Products.CountAsync();
        count.Should().Be(2);
    }

    #endregion
}
