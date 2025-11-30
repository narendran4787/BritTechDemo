using Application.DTOs;

namespace Application.Interfaces;

public interface IItemService
{
    Task<ItemDto?> GetByIdAsync(int productId, int itemId, CancellationToken ct = default);
    Task<ItemDto?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<ItemDto> UpsertAsync(int productId, int? itemId, ItemUpsertDto dto, CancellationToken ct = default);
}

