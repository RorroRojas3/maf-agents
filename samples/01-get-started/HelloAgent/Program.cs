using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Responses;

IConfiguration configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

// Env var form is OPENAI__APIKEY; OPENAI_API_KEY is mapped explicitly because that is the name
// the OpenAI tooling and CI systems already use.
string apiKey = configuration["OpenAI:ApiKey"]
    ?? configuration["OPENAI_API_KEY"]
    ?? throw new InvalidOperationException(
        """
        No OpenAI API key found. Set one with either:

          dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/01-get-started/HelloAgent

        or by exporting OPENAI_API_KEY in your environment.
        """);

string model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

AIAgent agent = new OpenAIClient(apiKey)
    .GetResponsesClient()
    .AsAIAgent(
        model: model,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "HelloAgent");

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("--- Run (complete response) ---");
AgentResponse response = await agent.RunAsync("What is the largest city in France?", cancellationToken: cts.Token);
Console.WriteLine(response);

Console.WriteLine();
Console.WriteLine("--- RunStreaming (token by token) ---");

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Tell me a one-sentence fun fact about it.", cancellationToken: cts.Token))
{
    Console.Write(update);
}

Console.WriteLine();
