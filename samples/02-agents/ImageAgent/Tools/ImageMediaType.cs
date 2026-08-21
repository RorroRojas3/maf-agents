namespace ImageAgent.Tools;

/// <summary>
/// Identifies image formats from their leading bytes.
/// </summary>
/// <remarks>
/// Sniffing beats trusting a file extension or an SDK metadata field: the extension the sample
/// writes then always matches what is actually in the file, and a mislabelled input is rejected
/// before it reaches the service.
/// </remarks>
internal static class ImageMediaType
{
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] _jpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _gifSignature = "GIF8"u8.ToArray();
    private static readonly byte[] _riffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] _webpSignature = "WEBP"u8.ToArray();

    /// <summary>Detects the media type of an image from its content.</summary>
    /// <param name="bytes">The candidate image bytes.</param>
    /// <returns>The media type, or <see langword="null"/> when the content is not a recognised image.</returns>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(_pngSignature))
        {
            return "image/png";
        }

        if (bytes.StartsWith(_jpegSignature))
        {
            return "image/jpeg";
        }

        if (bytes.StartsWith(_gifSignature))
        {
            return "image/gif";
        }

        if (bytes.Length >= 12 && bytes.StartsWith(_riffSignature) && bytes[8..12].SequenceEqual(_webpSignature))
        {
            return "image/webp";
        }

        return null;
    }

    /// <summary>Maps a media type to the file extension the sample writes.</summary>
    /// <param name="mediaType">A media type produced by <see cref="Detect"/>.</param>
    /// <returns>The extension, including the leading dot.</returns>
    public static string ToExtension(string mediaType) => mediaType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".bin",
    };
}
