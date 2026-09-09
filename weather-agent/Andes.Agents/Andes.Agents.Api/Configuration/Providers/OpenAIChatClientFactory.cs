using System.ClientModel;
using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Andes.Agents.Api.Configuration.Providers;

internal static class OpenAIChatClientFactory
{
    public static OpenAIClient CreateClient(OpenAIEndpointOptions options) =>
        new(new ApiKeyCredential(options.ApiKey), new OpenAIClientOptions { Endpoint = options.GetEndpointUri() });

    // Function invocation runs the tools; the telemetry layer above it emits the chat and tool spans.
    public static IChatClient Decorate(IChatClient chatClient, IServiceProvider provider)
    {
        ILoggerFactory loggerFactory = provider.GetRequiredService<ILoggerFactory>();
        TelemetryOptions telemetry = provider.GetRequiredService<IOptions<TelemetryOptions>>().Value;

        return chatClient
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory)
            .UseOpenTelemetry(loggerFactory, TelemetryNames.Source, otel => otel.EnableSensitiveData = telemetry.EnableSensitiveData)
            .Build(provider);
    }
}
