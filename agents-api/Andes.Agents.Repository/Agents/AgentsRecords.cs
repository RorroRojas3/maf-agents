namespace Andes.Agents.Repository.Agents;

/// <summary>An agent together with the model it currently runs on.</summary>
/// <param name="AgentId">Key of the agent.</param>
/// <param name="AgentName">The agent's registration name.</param>
/// <param name="ModelId">Key of the model.</param>
/// <param name="DeploymentName">Deployment the model is served from.</param>
/// <param name="Prices">The model's list prices.</param>
public sealed record ActiveAgentModel(Guid AgentId, string AgentName, Guid ModelId, string DeploymentName, TokenPrices Prices);

/// <summary>US dollar prices per million tokens.</summary>
/// <param name="InputPerMillionTokens">Price of uncached input tokens.</param>
/// <param name="CachedInputPerMillionTokens">Price of input tokens read from the provider's cache.</param>
/// <param name="OutputPerMillionTokens">Price of output tokens, reasoning tokens included.</param>
public sealed record TokenPrices(decimal InputPerMillionTokens, decimal CachedInputPerMillionTokens, decimal OutputPerMillionTokens)
{
    private const decimal _tokensPerPrice = 1_000_000m;

    // The scale of SessionSummary.EstimatedCost, so a cost is stored exactly as computed.
    private const int _costScale = 9;

    /// <summary>Returns the cost of the given tokens, rounded to the stored scale.</summary>
    /// <remarks><paramref name="cachedInputTokens"/> are part of <paramref name="inputTokens"/>, as the chat abstractions count them.</remarks>
    public decimal CostOf(long inputTokens, long cachedInputTokens, long outputTokens)
    {
        long input = Math.Max(0, inputTokens);
        long cached = Math.Clamp(cachedInputTokens, 0, input);
        long output = Math.Max(0, outputTokens);

        decimal cost = (((input - cached) * InputPerMillionTokens) + (cached * CachedInputPerMillionTokens) + (output * OutputPerMillionTokens)) / _tokensPerPrice;

        return decimal.Round(cost, _costScale, MidpointRounding.ToEven);
    }
}
