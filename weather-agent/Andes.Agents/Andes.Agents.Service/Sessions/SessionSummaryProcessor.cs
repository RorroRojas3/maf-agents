using Andes.Agents.Common.Constants;
using Andes.Agents.Repository.Agents;
using Andes.Agents.Repository.Sessions;
using Andes.Agents.Repository.Sessions.Interfaces;
using Andes.Agents.Service.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Andes.Agents.Service.Sessions;

/// <summary>Writes queued session counts to the reporting summaries in order, pricing them from the agent catalog.</summary>
/// <remarks>A failure is logged by exception type only: a store error can quote a summary's key, which holds the owner's object id.</remarks>
public sealed partial class SessionSummaryProcessor(
    ISessionSummaryChannel channel,
    IAgentCatalogCache catalogCache,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    TimeProvider timeProvider,
    ILogger<SessionSummaryProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan _firstRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _maxRetryDelay = TimeSpan.FromMinutes(5);

    private readonly ISessionSummaryChannel _channel = channel;
    private readonly IAgentCatalogCache _catalogCache = catalogCache;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<SessionSummaryProcessor> _logger = logger;

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Complete();

        // The base cancels the loop at once; waiting first lets the backlog drain within the host's shutdown deadline.
        if (ExecuteTask is { } running)
        {
            await running.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (SessionSummaryWork work in _channel.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await WriteAsync(work, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // The shutdown deadline cut the backlog short. Swallowed, because the host would log the exception text.
        }
    }

    #region Private Methods

    private async Task WriteAsync(SessionSummaryWork work, CancellationToken stoppingToken)
    {
        TimeSpan retryDelay = _firstRetryDelay;

        while (!await TryWriteAsync(work, retryDelay, stoppingToken).ConfigureAwait(false))
        {
            // A stopping host drains what it can write now instead of waiting out a store that is down.
            if (!await DelayUnlessStoppingAsync(retryDelay).ConfigureAwait(false))
            {
                LogDroppedWhileStopping(work.AgentName);

                return;
            }

            retryDelay = retryDelay * 2 < _maxRetryDelay ? retryDelay * 2 : _maxRetryDelay;
        }
    }

    // True once the work is written or dropped for good; false when it should be tried again.
    private async Task<bool> TryWriteAsync(SessionSummaryWork work, TimeSpan retryDelay, CancellationToken stoppingToken)
    {
        AgentCatalog catalog;

        try
        {
            catalog = await _catalogCache.GetAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogCatalogUnavailable(DescribeTypes(exception), retryDelay);

            return false;
        }

        if (catalog.Find(work.AgentName) is not { } model)
        {
            // Only a hosted agent is sure to have a model; a deleted document can name an agent this host no longer runs.
            if (!AgentNames.All.Contains(work.AgentName, StringComparer.OrdinalIgnoreCase))
            {
                LogAgentNotHosted(work.AgentName);

                return true;
            }

            // Startup proved a hosted agent has an active model, so this miss is a read that landed while its mapping was being swapped.
            _catalogCache.Invalidate();
            LogAgentNotInCatalog(work.AgentName, retryDelay);

            return false;
        }

        try
        {
            AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

            await using (scope.ConfigureAwait(false))
            {
                ISessionSummaryRepository summaries = scope.ServiceProvider.GetRequiredService<ISessionSummaryRepository>();

                await summaries.UpsertAsync(ToWrite(work, model), stoppingToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (SessionStoreUnavailableException exception)
        {
            LogStoreUnavailable(DescribeTypes(exception), retryDelay);

            return false;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogWriteDropped(DescribeTypes(exception));

            return true;
        }
    }

    private async Task<bool> DelayUnlessStoppingAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, _lifetime.ApplicationStopping).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            return false;
        }
    }

    private static SessionSummaryWrite ToWrite(SessionSummaryWork work, ActiveAgentModel model) => new(
        work.UserId,
        work.SessionId,
        work.DateCreated,
        model.AgentId,
        model.ModelId,
        model.Prices,
        work.DateModified,
        work.MessageCount,
        work.Usage,
        work.Details,
        work.DateDeleted);

    private static string DescribeTypes(Exception exception) =>
        exception.InnerException is null
            ? exception.GetType().Name
            : $"{exception.GetType().Name} ({exception.InnerException.GetType().Name})";

    #endregion

    #region Loggers

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent catalog could not be read ({ExceptionType}); retrying in {RetryDelay}.")]
    private partial void LogCatalogUnavailable(string exceptionType, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The agent catalog has no active model for {AgentName}; reloading it in {RetryDelay}.")]
    private partial void LogAgentNotInCatalog(string agentName, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A session summary for {AgentName}, an agent this host does not run, was dropped.")]
    private partial void LogAgentNotHosted(string agentName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session summary store is unavailable ({ExceptionType}); retrying in {RetryDelay}.")]
    private partial void LogStoreUnavailable(string exceptionType, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Error, Message = "A session summary was dropped after a failure that retrying cannot fix ({ExceptionType}).")]
    private partial void LogWriteDropped(string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A session summary for {AgentName} was dropped because the host is stopping.")]
    private partial void LogDroppedWhileStopping(string agentName);

    #endregion
}
