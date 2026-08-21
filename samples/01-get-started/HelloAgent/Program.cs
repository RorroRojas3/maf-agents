using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// The colon keys come from user-secrets (and from the double-underscore env form). The flat
// OPENAI_API_KEY / OPENAI_MODEL names are read too because that is what OpenAI tooling and CI
// systems already set. An unpopulated CI secret arrives as "" rather than absent, so these are
// whitespace checks and not null checks.
string apiKey = FirstNonBlank(configuration, "OpenAI:ApiKey", "OPENAI_API_KEY")
    ?? throw new InvalidOperationException(
        """
        No OpenAI API key found. Set one with either:

          dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/01-get-started/HelloAgent

        or by exporting OPENAI_API_KEY in your environment.
        """);

string model = FirstNonBlank(configuration, "OpenAI:Model", "OPENAI_MODEL") ?? "gpt-4o-mini";

#pragma warning disable OPENAI001 // GetResponsesClient is evaluation-only; see this sample's README.
AIAgent agent = new OpenAIClient(apiKey)
    .GetResponsesClient()
    .AsAIAgent(
        model: model,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "HelloAgent");
#pragma warning restore OPENAI001

using CancellationTokenSource cts = new();
Console.CancelKeyPress += OnCancelKeyPress;

try
{
    // Each run below is independent: no AgentSession is passed, so the agent carries nothing
    // from one call to the next. Conversation state is a later sample.
    Console.WriteLine("--- Run (complete response) ---");
    AgentResponse response = await agent.RunAsync("What is the largest city in France?", cancellationToken: cts.Token);
    Console.WriteLine(response.Text);

    Console.WriteLine();
    Console.WriteLine("--- RunStreaming (token by token) ---");

    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Tell me a one-sentence fun fact about France.", cancellationToken: cts.Token))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine();
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine("Cancelled.");
    return 130;
}
finally
{
    // The handler captures cts, so it has to go before cts is disposed.
    Console.CancelKeyPress -= OnCancelKeyPress;
}

return 0;

void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    e.Cancel = true;
    cts.Cancel();
}

static string? FirstNonBlank(IConfiguration configuration, params string[] keys)
{
    foreach (string key in keys)
    {
        string? value = configuration[key];

        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
    }

    return null;
}
