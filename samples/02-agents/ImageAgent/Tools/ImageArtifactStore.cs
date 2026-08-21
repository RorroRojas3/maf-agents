using System.Security.Cryptography;

namespace ImageAgent.Tools;

/// <summary>
/// Owns the per-run output folder and every byte written into it.
/// </summary>
/// <remarks>
/// File names are composed here from a caller-supplied label and a counter, never from anything the
/// model produced. A tool therefore cannot be talked into writing outside the run folder, and two
/// tools running in parallel — which this agent's model supports — cannot collide on a name.
/// </remarks>
/// <param name="rootDirectory">Directory that receives one timestamped folder per run.</param>
/// <param name="startedAt">Timestamp that names this run's folder.</param>
internal sealed class ImageArtifactStore(string rootDirectory, DateTimeOffset startedAt)
{
    private readonly string _runDirectory = Path.Combine(
        Path.GetFullPath(rootDirectory),
        startedAt.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));

    private readonly Lock _gate = new();
    private readonly List<string> _written = [];
    private int _sequence;

    /// <summary>Gets the absolute path of this run's output folder.</summary>
    public string RunDirectory => _runDirectory;

    /// <summary>Gets every file written so far, in the order it was written.</summary>
    public IReadOnlyList<string> Written
    {
        get
        {
            lock (_gate)
            {
                return [.. _written];
            }
        }
    }

    /// <summary>Writes bytes into the run folder under a generated name.</summary>
    /// <param name="label">Short label describing the artifact, such as <c>generated</c>.</param>
    /// <param name="extension">File extension, including the leading dot.</param>
    /// <param name="bytes">Content to write.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The absolute path of the written file.</returns>
    public async Task<string> SaveAsync(string label, string extension, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        string path = ReserveName(label, extension);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        Record(path);

        return path;
    }

    /// <summary>Writes text into the run folder under a generated name.</summary>
    /// <param name="label">Short label describing the artifact, such as <c>extracted</c>.</param>
    /// <param name="text">Content to write.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    /// <returns>The absolute path of the written file.</returns>
    public async Task<string> SaveTextAsync(string label, string text, CancellationToken cancellationToken)
    {
        string path = ReserveName(label, ".txt");
        await File.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);
        Record(path);

        return path;
    }

    /// <summary>Computes the lowercase hex SHA-256 of an artifact's bytes.</summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The hash as lowercase hexadecimal.</returns>
    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, hash);

        return Convert.ToHexStringLower(hash);
    }

    private string ReserveName(string label, string extension)
    {
        Directory.CreateDirectory(_runDirectory);

        int ordinal = Interlocked.Increment(ref _sequence);
        string safeLabel = string.Concat(label.Where(char.IsAsciiLetterOrDigit));
        string name = $"{ordinal:D2}-{(safeLabel.Length == 0 ? "artifact" : safeLabel)}{extension}";

        return Path.Combine(_runDirectory, name);
    }

    private void Record(string path)
    {
        lock (_gate)
        {
            _written.Add(path);
        }
    }
}
