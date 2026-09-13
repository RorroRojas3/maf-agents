using Andes.Agents.Entity.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Andes.Agents.Repository.Sql.Interceptors;

internal sealed class AuditTimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);

        return ValueTask.FromResult(result);
    }

    #region Private Methods

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Entries() detects changes itself, which matters here: interceptors run before SaveChanges detects them.
        foreach (EntityEntry<BaseEntity> entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                // A value the caller set is kept: a projection carries the timestamps of the record it copies.
                case EntityState.Added:
                    if (entry.Entity.DateCreated == default)
                    {
                        entry.Entity.DateCreated = now;
                    }

                    if (entry.Entity.DateModified == default)
                    {
                        entry.Entity.DateModified = now;
                    }

                    break;

                case EntityState.Modified:
                    if (!HasChanged(entry.Property(entity => entity.DateModified)))
                    {
                        entry.Entity.DateModified = now;
                    }

                    entry.Property(entity => entity.DateCreated).IsModified = false;
                    break;
            }
        }
    }

    // Against the original value, not IsModified: a disconnected Update marks every property modified.
    private static bool HasChanged(PropertyEntry<BaseEntity, DateTimeOffset> property) => property.CurrentValue != property.OriginalValue;

    #endregion
}
