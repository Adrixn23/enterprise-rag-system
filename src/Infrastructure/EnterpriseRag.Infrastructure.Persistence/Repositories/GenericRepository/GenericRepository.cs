namespace EnterpriseRag.Infrastructure.Persistence.Repositories.GenericRepository;

using EnterpriseRag.Core.Domain.Entities.Base;
using EnterpriseRag.Core.Domain.Interfaces.GenericRepository;
using EnterpriseRag.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity, TKey> where TEntity : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<TEntity>();
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        var affected = await _context.SaveChangesAsync(cancellationToken);
        return affected > 0;
    }

    public virtual async Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity is BaseEntity<TKey> baseEntity)
        {
            baseEntity.IsDeleted = true;
            baseEntity.UpdatedAt = DateTimeOffset.UtcNow;
            _dbSet.Update(entity);
        }
        else
        {
            _dbSet.Remove(entity);
        }

        var affected = await _context.SaveChangesAsync(cancellationToken);
        return affected > 0;
    }

    public virtual async Task<TEntity?> GetByIdAsync(TKey key, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object?[] { key }, cancellationToken: cancellationToken);
    }

    public virtual async Task<IReadOnlyCollection<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
    }
}
