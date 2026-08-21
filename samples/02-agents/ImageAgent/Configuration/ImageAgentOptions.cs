using Microsoft.Extensions.Logging;

namespace ImageAgent.Configuration;

/// <summary>Behavioural settings for the sample itself, as opposed to the Foundry connection.</summary>
internal sealed class ImageAgentOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "ImageAgent";

    /// <summary>
    /// Gets or sets the directory that receives one timestamped folder per run. Relative paths
    /// resolve against the current working directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>Gets or sets the image size applied when a request does not name one.</summary>
    public string DefaultImageSize { get; set; } = "1024x1024";

    /// <summary>Gets or sets the image quality applied when a request does not name one.</summary>
    public string DefaultQuality { get; set; } = "high";

    /// <summary>Gets or sets the minimum level at which framework diagnostics reach the console.</summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Warning;
}
