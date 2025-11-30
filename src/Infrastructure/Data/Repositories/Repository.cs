using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly DbSet<T> _dbSet;

    public Repository(DbContext context)
    {
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync([id], ct);

    public IQueryable<T> Query(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true)
    {
        IQueryable<T> query = _dbSet;
        if (predicate != null) query = query.Where(predicate);
        if (asNoTracking) query = query.AsNoTracking();
        return query;
    }

    public Task AddAsync(T entity, CancellationToken ct = default) => _dbSet.AddAsync(entity, ct).AsTask();

    public void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);
}
