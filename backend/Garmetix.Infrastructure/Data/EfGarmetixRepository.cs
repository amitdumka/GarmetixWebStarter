using Garmetix.Core.Interfaces;
using Garmetix.Core.Models.Base;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Infrastructure.Data;

public sealed class EfGarmetixRepository(GarmetixDbContext db) : IGarmetixRepository
{
    public async Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default) where T : class, IEntity
    {
        return await db.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IEntity
    {
        return await db.Set<T>().FindAsync([id], cancellationToken);
    }

    public async Task<T> SaveAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IEntity
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        var exists = await db.Set<T>().AsNoTracking().AnyAsync(item => item.Id == entity.Id, cancellationToken);
        db.Entry(entity).State = exists ? EntityState.Modified : EntityState.Added;

        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IEntity
    {
        var entity = await db.Set<T>().FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (entity is BaseEntity softDeletable)
        {
            softDeletable.Deleted = true;
            db.Entry(entity).State = EntityState.Modified;
        }
        else
        {
            db.Remove(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
