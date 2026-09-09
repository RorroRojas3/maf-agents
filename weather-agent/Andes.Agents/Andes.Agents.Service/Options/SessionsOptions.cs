using System.ComponentModel.DataAnnotations;

namespace Andes.Agents.Service.Options;

/// <summary>Settings of the persisted conversation history.</summary>
public sealed class SessionsOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Sessions";

    /// <summary>Gets or sets how many of the most recent messages are replayed to the model on each turn.</summary>
    [Range(1, 1000)]
    public int MaxHistoryMessages { get; set; } = 50;
}
