using System.ClientModel;
using ImageAgent.Configuration;
using ImageAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Images;
using OpenAI.Responses;

// sysexits.h conventions: the caller can tell a bad key from an unreachable service.
const int exitServiceError = 69;
const int exitConfigurationError = 78;
const int exitCancelled = 130;

const string instructions = """
    You are ImageAgent. You convert between text and images, and you do it with tools — never
    describe an image you have not read with a tool, and never claim to have drawn one you have not.

    Pick the tool that matches the request:
      - extract_text_from_image, when asked what an image says or shows, or to transcribe it.
      - generate_image_from_text, when asked for a new image.
      - transform_image, when asked to change an image that already exists.

    Every tool returns the absolute path of the file it wrote. Always tell the user that path, and
    reuse it verbatim when a follow-up request refers to an image from earlier in the conversation.
    When a tool reports a problem with a size, a format or a file, fix the argument and try again
    rather than repeating the same call.
    """;

IConfiguration configuration = ConfigurationLoader.Build();

FoundryOptions foundry;
ImageAgentOptions sampleOptions;

try
{
    foundry = ConfigurationLoader.LoadFoundryOptions(configuration);
    sampleOptions = ConfigurationLoader.LoadImageAgentOptions(configuration);
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);

    return exitConfigurationError;
}

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(sampleOptions.LogLevel));

ImageArtifactStore artifactStore = new(sampleOptions.OutputDirectory, DateTimeOffset.Now);

#pragma warning disable OPENAI001 // GetResponsesClient is evaluation-only; see this sample's README.
OpenAIClient client = new(
    new ApiKeyCredential(foundry.ApiKey),
    new OpenAIClientOptions { Endpoint = foundry.EndpointUri });

ResponsesClient responsesClient = client.GetResponsesClient();

// The GPT-5.6 series will not do tool calling over Chat Completions, so both the agent and the
// vision call below go through the Responses API rather than mixing the two.
IChatClient visionClient = responsesClient.AsIChatClient(foundry.ChatModel);
ImageClient imageClient = client.GetImageClient(foundry.ImageModel);

ImageAgentTools tools = new(imageClient, visionClient, artifactStore, sampleOptions);

AIAgent agent = responsesClient.AsAIAgent(
    model: foundry.ChatModel,
    instructions: instructions,
    name: "ImageAgent",
    description: "Reads images, draws new ones, and redraws existing ones.",
    tools: tools.CreateTools(),
    loggerFactory: loggerFactory);
#pragma warning restore OPENAI001

using CancellationTokenSource cts = new();
Console.CancelKeyPress += OnCancelKeyPress;

try
{
    WriteBanner(foundry, artifactStore);

    AgentSession session = await CreateSessionAsync();

    while (true)
    {
        Console.Write("> ");
        string? line = Console.ReadLine();

        // Null means end of input: the terminal closed, or the sample was fed a piped script.
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string command = line.Trim();

        if (command.Equals("/exit", StringComparison.OrdinalIgnoreCase)
            || command.Equals("/quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        if (command.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            WriteHelp();

            continue;
        }

        if (command.Equals("/output", StringComparison.OrdinalIgnoreCase))
        {
            WriteArtifacts(artifactStore, fromIndex: 0);

            continue;
        }

        if (command.Equals("/new", StringComparison.OrdinalIgnoreCase))
        {
            session = await CreateSessionAsync();
            Console.WriteLine("Started a new session. The agent no longer remembers earlier turns.");

            continue;
        }

        int artifactsBefore = artifactStore.Written.Count;

        try
        {
            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(line, session, cancellationToken: cts.Token))
            {
                Console.Write(update.Text);
            }

            Console.WriteLine();
            WriteArtifacts(artifactStore, artifactsBefore);
        }
        catch (ClientResultException ex)
        {
            Console.WriteLine();
            Console.Error.WriteLine(DescribeServiceFailure(ex));
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine("Cancelled.");

    return exitCancelled;
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);

    return exitServiceError;
}
finally
{
    // The handler captures cts, so it has to go before cts is disposed.
    Console.CancelKeyPress -= OnCancelKeyPress;
}

Console.WriteLine($"Artifacts for this run are in {artifactStore.RunDirectory}");

return 0;

async Task<AgentSession> CreateSessionAsync()
{
    try
    {
        return await agent.CreateSessionAsync(cts.Token);
    }
    catch (ClientResultException ex)
    {
        throw new InvalidOperationException(DescribeServiceFailure(ex), ex);
    }
}

void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cts.Cancel();
}

static void WriteBanner(FoundryOptions foundry, ImageArtifactStore artifactStore)
{
    Console.WriteLine("ImageAgent — text to image, image to text, image to image.");
    Console.WriteLine($"  endpoint : {foundry.EndpointUri.GetLeftPart(UriPartial.Path)}");
    Console.WriteLine($"  chat     : {foundry.ChatModel}");
    Console.WriteLine($"  image    : {foundry.ImageModel}");
    Console.WriteLine($"  output   : {artifactStore.RunDirectory}");
    Console.WriteLine();
    WriteHelp();
}

static void WriteHelp()
{
    Console.WriteLine("""
        Ask in plain language — "draw a watercolour fox", "what does C:\scan.png say",
        "now make that one a night scene". The agent chooses the tool.

          /new      start a fresh session, forgetting earlier turns
          /output   list every file written this run
          /help     show this
          /exit     quit

        Ctrl+C cancels an in-flight request and exits with code 130.
        """);
    Console.WriteLine();
}

static void WriteArtifacts(ImageArtifactStore artifactStore, int fromIndex)
{
    IReadOnlyList<string> written = artifactStore.Written;

    if (written.Count <= fromIndex)
    {
        return;
    }

    Console.WriteLine();

    for (int index = fromIndex; index < written.Count; index++)
    {
        Console.WriteLine($"  saved: {written[index]}");
    }

    Console.WriteLine();
}

static string DescribeServiceFailure(ClientResultException exception) => exception.Status switch
{
    401 or 403 => $"The service rejected the credential ({exception.Status}). Check Foundry:ApiKey and that the key belongs to the configured endpoint.",
    404 => $"The service returned 404. Check that Foundry:ChatModel and Foundry:ImageModel name deployments that exist on this resource — the v1 route wants the deployment name, not the model name.",

    // Image deployments default to five images per minute on Foundry, which a couple of quick
    // follow-up requests will hit.
    429 => $"Rate limited ({exception.Status}). Image deployments default to 5 images/minute — wait a moment and ask again.",
    _ => $"The service call failed ({exception.Status}): {exception.Message}",
};
