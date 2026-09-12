using System.Runtime.CompilerServices;
using Andes.Agents.Common.Constants;
using Andes.Agents.Entity.Sessions;
using Andes.Agents.Service.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Andes.Agents.Service.Agents;

/// <summary>Adds each turn's token usage to the session's running totals and logs it.</summary>
public sealed partial class UsageRecordingAgent(AIAgent innerAgent, ILoggerFactory loggerFactory) : DelegatingAIAgent(innerAgent)
{
    private readonly ILogger<UsageRecordingAgent> _logger = loggerFactory.CreateLogger<UsageRecordingAgent>();

    /// <inheritdoc />
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        AgentResponse response = await InnerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);

        Record(session, response.Usage);

        return response;
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UsageDetails? total = null;

        try
        {
            await foreach (AgentResponseUpdate update in InnerAgent.RunStreamingAsync(messages, session, options, cancellationToken).ConfigureAwait(false))
            {
                foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
                {
                    total ??= new UsageDetails();
                    total.Add(usage.Details);
                }

                yield return update;
            }
        }
        finally
        {
            // The provider bills for a stream the client abandoned just the same.
            Record(session, total);
        }
    }

    #region Private Methods

    private void Record(AgentSession? session, UsageDetails? usage)
    {
        if (usage is null)
        {
            return;
        }

        long input = usage.InputTokenCount ?? 0;
        long output = usage.OutputTokenCount ?? 0;
        long total = usage.TotalTokenCount ?? input + output;

        LogUsage(Name ?? Id, input, output, total);

        if (session is null)
        {
            return;
        }

        SessionUsage current = session.StateBag.GetValue<SessionUsage>(SessionStateKeys.Usage, SessionStateJson.Options) ?? SessionUsage.Empty;
        session.StateBag.SetValue(SessionStateKeys.Usage, current.Add(input, output, total), SessionStateJson.Options);

        SessionUsageDetails details = session.StateBag.GetValue<SessionUsageDetails>(SessionStateKeys.UsageDetails, SessionStateJson.Options) ?? SessionUsageDetails.Empty;
        session.StateBag.SetValue(
            SessionStateKeys.UsageDetails,
            details.Add(usage.CachedInputTokenCount ?? 0, usage.ReasoningTokenCount ?? 0),
            SessionStateJson.Options);
    }

    #endregion

    #region Loggers

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentName} used {InputTokens} input and {OutputTokens} output tokens ({TotalTokens} total).")]
    private partial void LogUsage(string agentName, long inputTokens, long outputTokens, long totalTokens);

    #endregion
}
