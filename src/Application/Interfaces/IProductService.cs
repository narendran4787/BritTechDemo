using Application.DTOs;

namespace Application.Interfaces;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(IEnumerable<ProductDto> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(ProductCreateDto dto, string createdBy, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, ProductUpdateDto dto, string modifiedBy, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
