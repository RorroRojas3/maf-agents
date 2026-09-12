using Andes.Agents.Entity.Sessions;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Andes.Agents.Repository.Sql.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Andes.Agents.Repository.Sql.Sessions;

/// <inheritdoc />
public sealed class SqlSessionSummaryRepository(PolicyDbContext ctx) : ISessionSummaryRepository
{
    private const int _maxAttempts = 3;

    private readonly PolicyDbContext _ctx = ctx;

    /// <inheritdoc />
    public async Task UpsertAsync(SessionSummaryWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        for (int attempt = 1; ; attempt++)
        {
            // A retry must start from the row the other writer saved, not replay this attempt's tracked changes.
            _ctx.ChangeTracker.Clear();

            try
            {
                await UpsertOnceAsync(write, cancellationToken).ConfigureAwait(false);

                return;
            }
            catch (RetryLimitExceededException ex)
            {
                throw new SessionStoreUnavailableException("The session summary store stayed unreachable through its retries.", ex);
            }
            catch (Exception ex) when (SqlErrors.IsStoreUnavailable(ex))
            {
                throw new SessionStoreUnavailableException("The session summary store is unavailable.", ex);
            }
            catch (DbUpdateException ex) when (attempt == _maxAttempts && IsLostRace(ex))
            {
                throw new SessionConflictException("Concurrent writers kept changing the session summary.", ex);
            }
            catch (DbUpdateException ex) when (IsLostRace(ex))
            {
                // Another writer saved the same incarnation between this attempt's read and its save.
            }
        }
    }

    private static bool IsLostRace(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException || SqlErrors.IsUniqueKeyViolation(exception);

    // Every count only grows within an incarnation, so a lower one marks a write that lost the race to a newer one.
    private static void MergeCounts(SessionSummary summary, SessionSummaryWrite write)
    {
        SessionUsage usage = write.Usage;

        // A deletion does not know the details, so it leaves the stored ones in place.
        long cachedInputTokens = write.Details?.CachedInputTokens ?? summary.CachedInputTokens;
        long reasoningTokens = write.Details?.ReasoningTokens ?? summary.ReasoningTokens;

        bool isOlder = write.MessageCount < summary.MessageCount
            || usage.InputTokens < summary.InputTokens
            || cachedInputTokens < summary.CachedInputTokens
            || usage.OutputTokens < summary.OutputTokens
            || reasoningTokens < summary.ReasoningTokens
            || usage.TotalTokens < summary.TotalTokens;

        bool grew = write.MessageCount > summary.MessageCount
            || usage.InputTokens > summary.InputTokens
            || cachedInputTokens > summary.CachedInputTokens
            || usage.OutputTokens > summary.OutputTokens
            || reasoningTokens > summary.ReasoningTokens
            || usage.TotalTokens > summary.TotalTokens;

        if (isOlder || !grew)
        {
            return;
        }

        summary.EstimatedCost += write.Prices.CostOf(
            usage.InputTokens - summary.InputTokens,
            cachedInputTokens - summary.CachedInputTokens,
            usage.OutputTokens - summary.OutputTokens);

        summary.AgentId = write.AgentId;
        summary.ModelId = write.ModelId;
        summary.MessageCount = write.MessageCount;
        summary.InputTokens = usage.InputTokens;
        summary.CachedInputTokens = cachedInputTokens;
        summary.OutputTokens = usage.OutputTokens;
        summary.ReasoningTokens = reasoningTokens;
        summary.TotalTokens = usage.TotalTokens;
        summary.DateModified = write.DateModified;
    }

    private async Task UpsertOnceAsync(SessionSummaryWrite write, CancellationToken cancellationToken)
    {
        SessionSummary? summary = await _ctx.SessionSummaries
            .SingleOrDefaultAsync(
                row => row.UserId == write.UserId && row.SessionId == write.SessionId && row.DateCreated == write.DateCreated,
                cancellationToken)
            .ConfigureAwait(false);

        if (summary is null)
        {
            summary = new SessionSummary
            {
                UserId = write.UserId,
                SessionId = write.SessionId,
                AgentId = write.AgentId,
                ModelId = write.ModelId,
                DateCreated = write.DateCreated,
                DateModified = write.DateModified,
            };

            _ctx.SessionSummaries.Add(summary);
        }

        MergeCounts(summary, write);

        if (write.DateDeleted is { } dateDeleted && summary.DateDeleted is null)
        {
            summary.DateDeleted = dateDeleted;
            summary.DateModified = dateDeleted;
        }

        await _ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
