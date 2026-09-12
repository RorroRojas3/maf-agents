namespace Andes.Agents.Api.Options;

/// <summary>Browser origins allowed to call the API; empty allows none.</summary>
public sealed class CorsOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "Cors";

    /// <summary>Gets the allowed origins, scheme and host each, such as <c>https://app.example.com</c>.</summary>
    public IList<string> AllowedOrigins { get; } = [];
}
