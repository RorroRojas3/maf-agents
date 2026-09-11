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
                case EntityState.Added:
                    entry.Entity.DateCreated = now;
                    entry.Entity.DateUpdated = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.DateUpdated = now;
                    entry.Property(entity => entity.DateCreated).IsModified = false;
                    break;
            }
        }
    }
}
