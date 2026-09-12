namespace Andes.Agents.Common.Constants;

/// <summary>Keys under which this application keeps its own records in an agent session's state bag.</summary>
public static class SessionStateKeys
{
    /// <summary>Identity and running summary of the persisted chat history.</summary>
    public const string History = "andes.history";

    /// <summary>Cumulative token usage of the session.</summary>
    public const string Usage = "andes.usage";

    /// <summary>Cumulative cached input and reasoning tokens; a key of its own, so a build that only knows <see cref="Usage"/> carries it through unchanged.</summary>
    public const string UsageDetails = "andes.usage.details";
}
