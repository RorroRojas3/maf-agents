using System.Globalization;
using OpenAI.Images;

namespace ImageAgent.Tools;

/// <summary>
/// Turns the free-text size and quality values a model supplies into typed request options.
/// </summary>
/// <remarks>
/// The size rules are checked here rather than left to the service. A rejected size comes back as a
/// generic HTTP 400, which the model tends to retry verbatim; a local failure names the actual
/// constraint so the next attempt can satisfy it.
/// </remarks>
internal static class ImageRequestParser
{
    private const int _edgeMultiple = 16;
    private const int _maxLongEdge = 3840;
    private const int _minTotalPixels = 655_360;
    private const int _maxTotalPixels = 8_294_400;
    private const double _maxAspectRatio = 3.0;

    /// <summary>Parses a size such as <c>1024x1024</c>.</summary>
    /// <param name="size">The requested size, or <c>auto</c>, empty or <see langword="null"/>.</param>
    /// <returns>The parsed size, or <see langword="null"/> to let the service choose.</returns>
    /// <exception cref="ArgumentException">The size is malformed or violates a documented constraint.</exception>
    public static GeneratedImageSize? ParseSize(string? size)
    {
        if (string.IsNullOrWhiteSpace(size) || size.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] parts = size.Trim().Split('x', 'X', '×');

        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
            || width <= 0
            || height <= 0)
        {
            throw new ArgumentException($"Size must look like '1024x1024' or be 'auto'. Got '{size}'.", nameof(size));
        }

        Validate(width, height, size);

        return new GeneratedImageSize(width, height);
    }

    /// <summary>Parses a quality such as <c>high</c>.</summary>
    /// <param name="quality">The requested quality, or <c>auto</c>, empty or <see langword="null"/>.</param>
    /// <returns>The parsed quality, or <see langword="null"/> to let the service choose.</returns>
    /// <exception cref="ArgumentException">The quality is not one the image models accept.</exception>
    public static GeneratedImageQuality? ParseQuality(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality) || quality.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // The GPT-image tiers are the evaluation-only members of this type; Standard and High are
        // the older DALL-E pair and are not what these models accept.
#pragma warning disable OPENAI001
        return quality.Trim().ToLowerInvariant() switch
        {
            "low" => GeneratedImageQuality.LowQuality,
            "medium" => GeneratedImageQuality.MediumQuality,
            "high" => GeneratedImageQuality.HighQuality,
            _ => throw new ArgumentException($"Quality must be 'low', 'medium', 'high' or 'auto'. Got '{quality}'.", nameof(quality)),
        };
#pragma warning restore OPENAI001
    }

    private static void Validate(int width, int height, string original)
    {
        if (width % _edgeMultiple != 0 || height % _edgeMultiple != 0)
        {
            throw new ArgumentException($"Both edges must be a multiple of {_edgeMultiple} pixels. Got '{original}'.", nameof(original));
        }

        int longEdge = Math.Max(width, height);
        int shortEdge = Math.Min(width, height);

        if (longEdge > _maxLongEdge)
        {
            throw new ArgumentException($"The long edge must be at most {_maxLongEdge} pixels. Got '{original}'.", nameof(original));
        }

        if ((double)longEdge / shortEdge > _maxAspectRatio)
        {
            throw new ArgumentException($"The aspect ratio must be at most 3:1. Got '{original}'.", nameof(original));
        }

        long totalPixels = (long)width * height;

        if (totalPixels < _minTotalPixels || totalPixels > _maxTotalPixels)
        {
            throw new ArgumentException(
                $"Total pixels must be between {_minTotalPixels:N0} and {_maxTotalPixels:N0}. '{original}' is {totalPixels:N0}.",
                nameof(original));
        }
    }
}
