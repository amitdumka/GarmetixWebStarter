using Garmetix.Core.Interfaces;

namespace Garmetix.Infrastructure.Data;

public interface IGarmetixRepository
{
    Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken cancellationToken = default) where T : class, IEntity;
    Task<T?> FindAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IEntity;
    Task<T> SaveAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, IEntity;
    Task<bool> DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default) where T : class, IEntity;
}
