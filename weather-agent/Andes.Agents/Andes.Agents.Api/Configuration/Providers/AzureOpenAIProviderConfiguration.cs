using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Andes.Agents.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace Andes.Agents.Api.Configuration.Providers;

internal static class AzureOpenAIProviderConfiguration
{
    public static IServiceCollection AddAzureOpenAIProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<AzureOpenAIOptions>, AzureOpenAIOptionsValidator>();

        services
            .AddOptions<AzureOpenAIOptions>()
            .Bind(configuration.GetSection(AzureOpenAIOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddKeyedSingleton<IChatClient>(ChatClientKeys.AzureOpenAI, (provider, _) =>
        {
            AzureOpenAIOptions azureOpenAI = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

#pragma warning disable OPENAI001, MAAI001 // The Responses API and its stored-output-disabled adapter are still evaluation-only.
            IChatClient chatClient = OpenAIChatClientFactory
                .CreateClient(azureOpenAI)
                .GetResponsesClient()
                // store:false keeps Cosmos DB the only conversation state, which the agent requires when it owns the history.
                .AsIChatClientWithStoredOutputDisabled(azureOpenAI.Model);
#pragma warning restore OPENAI001, MAAI001

            return OpenAIChatClientFactory.Decorate(chatClient, provider);
        });

        return services;
    }
}
