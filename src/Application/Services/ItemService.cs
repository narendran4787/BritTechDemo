using Application.DTOs;
using Application.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ItemService : IItemService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ItemService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<ItemDto?> GetByIdAsync(int productId, int itemId, CancellationToken ct = default)
    {
        var query = _uow.Repository<Item>().Query(
            i => i.Id == itemId && i.ProductId == productId);
        return await query.ProjectTo<ItemDto>(_mapper.ConfigurationProvider).FirstOrDefaultAsync(ct);
    }

    public async Task<ItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default)
    {
        var query = _uow.Repository<Item>().Query(i => i.ProductId == productId);
        return await query.ProjectTo<ItemDto>(_mapper.ConfigurationProvider).FirstOrDefaultAsync(ct);
    }

    public async Task<ItemDto> UpsertAsync(int productId, int? itemId, ItemUpsertDto dto, CancellationToken ct = default)
    {
        // Verify product exists
        var productRepo = _uow.Repository<Domain.Entities.Product>();
        var product = await productRepo.GetByIdAsync(productId, ct);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId} not found.");
        }

        var itemRepo = _uow.Repository<Item>();
        
        // Check if an item already exists for this product (enforcing uniqueness per product)
        var existingItem = await itemRepo.Query(i => i.ProductId == productId, asNoTracking: false)
            .FirstOrDefaultAsync(ct);

        Item entity;

        if (existingItem != null)
        {
            // Item already exists for this product - update it
            // If itemId was provided, verify it matches
            if (itemId.HasValue && existingItem.Id != itemId.Value)
            {
                throw new InvalidOperationException(
                    $"An item already exists for product {productId} with ID {existingItem.Id}. " +
                    $"Cannot create/update item with different ID {itemId.Value}.");
            }
            
            // Update existing item
            existingItem.Quantity = dto.Quantity;
            itemRepo.Update(existingItem);
            entity = existingItem;
        }
        else
        {
            // No item exists for this product - create new one
            entity = new Item
            {
                ProductId = productId,
                Quantity = dto.Quantity
            };
            
            // If itemId was provided, use it (though it's typically auto-generated)
            if (itemId.HasValue)
            {
                entity.Id = itemId.Value;
            }
            
            await itemRepo.AddAsync(entity, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ItemDto>(entity);
    }
}

