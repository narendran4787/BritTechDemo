using System.Linq.Expressions;

namespace Infrastructure.Data.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    IQueryable<T> Query(Expression<Func<T, bool>>? predicate = null, bool asNoTracking = true);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}
