namespace Andes.Agents.Api.Options;

/// <summary>Where telemetry goes and how much of the conversation it may carry.</summary>
public sealed class TelemetryOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Telemetry";

    /// <summary>Gets or sets the Application Insights connection string; blank keeps telemetry in-process only.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets whether ingestion authenticates with the shared Azure credential instead of the instrumentation key alone.</summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>Gets or sets whether prompts, completions and tool arguments are recorded on spans.</summary>
    public bool EnableSensitiveData { get; set; }
}
