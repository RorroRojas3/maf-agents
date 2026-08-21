namespace ImageAgent.Tools;

/// <summary>
/// What a tool reports back to the model after writing an image.
/// </summary>
/// <remarks>
/// Deliberately compact. The image itself never travels through the model's context — base64 of a
/// 4K image is megabytes of tokens, which would evict the conversation and can be silently
/// truncated. The model gets a pointer plus enough metadata to talk about the result; the bytes
/// stay on disk at full fidelity.
/// </remarks>
/// <param name="Path">Absolute path of the written file.</param>
/// <param name="MediaType">Media type detected from the file's own bytes.</param>
/// <param name="ByteCount">Exact size of the written file.</param>
/// <param name="Sha256">Lowercase hex SHA-256 of the bytes, so the artifact can be verified.</param>
/// <param name="RequestedSize">Size asked of the service, or <c>auto</c>.</param>
/// <param name="RequestedQuality">Quality asked of the service, or <c>auto</c>.</param>
/// <param name="RevisedPrompt">The prompt the service actually drew from, when it rewrote the one it was given.</param>
/// <param name="SourcePath">Absolute path of the input image, for transforms.</param>
internal sealed record ImageArtifactResult(
    string Path,
    string MediaType,
    long ByteCount,
    string Sha256,
    string RequestedSize,
    string RequestedQuality,
    string? RevisedPrompt = null,
    string? SourcePath = null);

/// <summary>
/// What the image-to-text tool reports back to the model.
/// </summary>
/// <param name="SourcePath">Absolute path of the image that was read.</param>
/// <param name="MediaType">Media type detected from the file's own bytes.</param>
/// <param name="ByteCount">Size of the image that was read.</param>
/// <param name="Text">The extracted text, complete and unabridged.</param>
/// <param name="TranscriptPath">Absolute path of the file the text was also written to.</param>
internal sealed record ImageTextResult(
    string SourcePath,
    string MediaType,
    long ByteCount,
    string Text,
    string TranscriptPath);
