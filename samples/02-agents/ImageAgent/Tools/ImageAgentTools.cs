using System.ComponentModel;
using ImageAgent.Configuration;
using Microsoft.Extensions.AI;
using OpenAI.Images;
using ImageGenerationOptions = OpenAI.Images.ImageGenerationOptions;

namespace ImageAgent.Tools;

/// <summary>
/// The three function tools that make up the agent: read an image, draw one, redraw one.
/// </summary>
/// <remarks>
/// Each tool validates its inputs against the documented service limits before making a call. A
/// local failure carries the actual constraint in its message, which the model can act on; the
/// generic HTTP 400 the service would return instead tends to be retried unchanged.
/// </remarks>
/// <param name="imageClient">Client bound to the image deployment that draws and edits pixels.</param>
/// <param name="visionClient">Client bound to the chat deployment that reads images.</param>
/// <param name="artifactStore">Destination for every byte the tools produce.</param>
/// <param name="options">Defaults applied when a request does not specify size or quality.</param>
internal sealed class ImageAgentTools(
    ImageClient imageClient,
    IChatClient visionClient,
    ImageArtifactStore artifactStore,
    ImageAgentOptions options)
{
    private const long _maxVisionInputBytes = 20L * 1024 * 1024;
    private const long _maxEditInputBytes = 50L * 1024 * 1024;
    private const long _maxMaskBytes = 4L * 1024 * 1024;

    private const string _defaultExtractionInstruction =
        "Transcribe every piece of text visible in this image exactly as it appears, then describe "
        + "the image in enough detail that someone who cannot see it could picture it.";

    private static readonly string[] _visionMediaTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];
    private static readonly string[] _editMediaTypes = ["image/png", "image/jpeg"];
    private static readonly string[] _maskMediaTypes = ["image/png"];

    private readonly ImageClient _imageClient = imageClient;
    private readonly IChatClient _visionClient = visionClient;
    private readonly ImageArtifactStore _artifactStore = artifactStore;
    private readonly ImageAgentOptions _options = options;

    /// <summary>Builds the tool list handed to the agent.</summary>
    /// <returns>The three tools, named as the model sees them.</returns>
    public IList<AITool> CreateTools() =>
    [
        AIFunctionFactory.Create(
            ExtractTextFromImageAsync,
            name: "extract_text_from_image",
            description: "Read an image on disk and return its text and a description of what it shows."),
        AIFunctionFactory.Create(
            GenerateImageFromTextAsync,
            name: "generate_image_from_text",
            description: "Draw a brand new image from a text description and save it to disk."),
        AIFunctionFactory.Create(
            TransformImageAsync,
            name: "transform_image",
            description: "Redraw an existing image on disk according to a text instruction and save the result."),
    ];

    [Description("Read an image file and return everything it says and shows. Use this whenever the user asks what is in an image, to transcribe it, or to describe it.")]
    private async Task<ImageTextResult> ExtractTextFromImageAsync(
        [Description("Absolute or relative path to the image file to read.")] string imagePath,
        [Description("Optional instruction narrowing what to extract, such as only the handwritten notes.")] string? instruction,
        CancellationToken cancellationToken)
    {
        (string fullPath, byte[] bytes, string mediaType) = await ReadImageAsync(
            imagePath,
            _maxVisionInputBytes,
            _visionMediaTypes,
            nameof(imagePath),
            cancellationToken).ConfigureAwait(false);

        string prompt = string.IsNullOrWhiteSpace(instruction) ? _defaultExtractionInstruction : instruction;

        ChatMessage message = new(ChatRole.User, [new TextContent(prompt), new DataContent(bytes, mediaType)]);
        ChatResponse response = await _visionClient
            .GetResponseAsync([message], cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        string text = response.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"The model returned no text for '{fullPath}'.");
        }

        // Written out as well as returned: the return value is what the model sees, and the file is
        // what survives the process for whoever asked.
        string transcriptPath = await _artifactStore
            .SaveTextAsync("extracted", text, cancellationToken)
            .ConfigureAwait(false);

        return new ImageTextResult(fullPath, mediaType, bytes.LongLength, text, transcriptPath);
    }

    [Description("Draw a new image from a text description. Use this when the user wants an image created from nothing.")]
    private async Task<ImageArtifactResult> GenerateImageFromTextAsync(
        [Description("Full description of the image to draw. Describe subject, composition and visual style.")] string prompt,
        [Description("Optional size such as 1024x1024 or 1536x1024, or auto. Both edges must be multiples of 16.")] string? size,
        [Description("Optional quality: low, medium, high or auto.")] string? quality,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        string requestedSize = Coalesce(size, _options.DefaultImageSize);
        string requestedQuality = Coalesce(quality, _options.DefaultQuality);

        ImageGenerationOptions generationOptions = new();

        if (ImageRequestParser.ParseSize(requestedSize) is { } parsedSize)
        {
            generationOptions.Size = GeneratedImageSize.W256xH256;
        }

#pragma warning disable OPENAI001 // ImageEditOptions.Quality is evaluation-only; its generation-side twin is not.
        if (ImageRequestParser.ParseQuality(requestedQuality) is { } parsedQuality)
        {
            generationOptions.Quality = GeneratedImageQuality.LowQuality;
        }
#pragma warning restore OPENAI001

        GeneratedImage image = await _imageClient
            .GenerateImageAsync(prompt, generationOptions, cancellationToken)
            .ConfigureAwait(false);

        return await SaveGeneratedImageAsync(
            image,
            "generated",
            requestedSize,
            requestedQuality,
            sourcePath: null,
            cancellationToken).ConfigureAwait(false);
    }

    [Description("Redraw an existing image according to an instruction. Use this to edit, restyle or extend an image the user already has.")]
    private async Task<ImageArtifactResult> TransformImageAsync(
        [Description("Absolute or relative path to the source image. Must be PNG or JPEG.")] string imagePath,
        [Description("What to change about the image, such as making it a night scene with a full moon.")] string prompt,
        [Description("Optional path to a PNG mask whose fully transparent pixels mark the region to edit.")] string? maskPath,
        [Description("Optional size such as 1024x1024, or auto. Both edges must be multiples of 16.")] string? size,
        [Description("Optional quality: low, medium, high or auto.")] string? quality,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        (string fullPath, byte[] sourceBytes, string sourceMediaType) = await ReadImageAsync(
            imagePath,
            _maxEditInputBytes,
            _editMediaTypes,
            nameof(imagePath),
            cancellationToken).ConfigureAwait(false);

        string requestedSize = Coalesce(size, _options.DefaultImageSize);
        string requestedQuality = Coalesce(quality, _options.DefaultQuality);

        ImageEditOptions editOptions = new();

        if (ImageRequestParser.ParseSize(requestedSize) is { } parsedSize)
        {
            editOptions.Size = GeneratedImageSize.W256xH256;
        }

#pragma warning disable OPENAI001 // ImageEditOptions.Quality is evaluation-only; its generation-side twin is not.
        if (ImageRequestParser.ParseQuality(requestedQuality) is { } parsedQuality)
        {
            editOptions.Quality = GeneratedImageQuality.LowQuality;
        }
#pragma warning restore OPENAI001

        using MemoryStream sourceStream = new(sourceBytes, writable: false);

        // The service reads the format from this extension, so it is derived from the bytes rather
        // than from the name on disk — a PNG saved as ".jpeg" passes the sniff and would fail here.
        string sourceFileName = $"source{ImageMediaType.ToExtension(sourceMediaType)}";

        GeneratedImage image;

        if (string.IsNullOrWhiteSpace(maskPath))
        {
            image = await _imageClient
                .GenerateImageEditAsync(sourceStream, sourceFileName, prompt, editOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            (_, byte[] maskBytes, string maskMediaType) = await ReadImageAsync(
                maskPath,
                _maxMaskBytes,
                _maskMediaTypes,
                nameof(maskPath),
                cancellationToken).ConfigureAwait(false);

            using MemoryStream maskStream = new(maskBytes, writable: false);

            image = await _imageClient
                .GenerateImageEditAsync(
                    sourceStream,
                    sourceFileName,
                    prompt,
                    maskStream,
                    $"mask{ImageMediaType.ToExtension(maskMediaType)}",
                    editOptions,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await SaveGeneratedImageAsync(
            image,
            "transformed",
            requestedSize,
            requestedQuality,
            fullPath,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImageArtifactResult> SaveGeneratedImageAsync(
        GeneratedImage image,
        string label,
        string requestedSize,
        string requestedQuality,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        if (image.ImageBytes is null)
        {
            throw new InvalidOperationException(
                "The service returned an image reference rather than image data. The GPT-image models always "
                + "return base64, so this points at a non-image deployment configured as Foundry:ImageModel.");
        }

        ReadOnlyMemory<byte> bytes = image.ImageBytes.ToMemory();
        string mediaType = ImageMediaType.Detect(bytes.Span)
            ?? throw new InvalidOperationException("The service returned data that is not a recognised image format.");

        string path = await _artifactStore
            .SaveAsync(label, ImageMediaType.ToExtension(mediaType), bytes, cancellationToken)
            .ConfigureAwait(false);

        return new ImageArtifactResult(
            path,
            mediaType,
            bytes.Length,
            ImageArtifactStore.ComputeSha256(bytes.Span),
            requestedSize,
            requestedQuality,
            image.RevisedPrompt,
            sourcePath);
    }

    private static async Task<(string FullPath, byte[] Bytes, string MediaType)> ReadImageAsync(
        string path,
        long maxBytes,
        string[] allowedMediaTypes,
        string parameterName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No image at '{fullPath}'.", fullPath);
        }

        long length = new FileInfo(fullPath).Length;

        if (length > maxBytes)
        {
            throw new ArgumentException(
                $"'{fullPath}' is {length / (1024 * 1024)} MB; the limit for this operation is {maxBytes / (1024 * 1024)} MB.",
                parameterName);
        }

        byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        string? mediaType = ImageMediaType.Detect(bytes);

        if (mediaType is null || !allowedMediaTypes.Contains(mediaType))
        {
            throw new ArgumentException(
                $"'{fullPath}' is not one of the accepted formats ({string.Join(", ", allowedMediaTypes)}).",
                parameterName);
        }

        return (fullPath, bytes, mediaType);
    }

    private static string Coalesce(string? requested, string fallback) =>
        string.IsNullOrWhiteSpace(requested) ? fallback : requested;
}
