using Application.DTOs;
using Application.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var query = _uow.Repository<Product>().Query(p => p.Id == id);
        return await query.ProjectTo<ProductDto>(_mapper.ConfigurationProvider).FirstOrDefaultAsync(ct);
    }

    public async Task<(IEnumerable<ProductDto> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        var query = _uow.Repository<Product>().Query();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<ProductDto>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, string createdBy, CancellationToken ct = default)
    {
        var entity = new Product
        {
            ProductName = dto.ProductName,
            CreatedBy = createdBy,
            CreatedOn = DateTime.UtcNow
        };
        await _uow.Repository<Product>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return _mapper.Map<ProductDto>(entity);
    }

    public async Task<bool> UpdateAsync(int id, ProductUpdateDto dto, string modifiedBy, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Product>();
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity == null) return false;
        entity.ProductName = dto.ProductName;
        entity.ModifiedBy = modifiedBy;
        entity.ModifiedOn = DateTime.UtcNow;
        repo.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Product>();
        var entity = await repo.GetByIdAsync(id, ct);
        if (entity == null) return false;
        repo.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
