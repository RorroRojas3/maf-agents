using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Andes.Agents.Common.Validation;
using FluentValidation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Configuration.Providers;

internal static class MicrosoftFoundryProviderConfiguration
{
    public static IServiceCollection AddMicrosoftFoundryProvider(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection(MicrosoftFoundryOptions.SectionName);

        // Nothing requires a completions deployment yet, so a section with no endpoint registers no client
        // instead of failing startup; naming an endpoint opts into full validation of the rest.
        if (string.IsNullOrWhiteSpace(section[nameof(OpenAIEndpointOptions.Endpoint)]))
        {
            return services;
        }

        services.AddSingleton<IValidator<MicrosoftFoundryOptions>, MicrosoftFoundryOptionsValidator>();

        services
            .AddOptions<MicrosoftFoundryOptions>()
            .Bind(section)
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddKeyedSingleton<IChatClient>(ChatClientKeys.MicrosoftFoundry, (provider, _) =>
        {
            MicrosoftFoundryOptions foundry = provider.GetRequiredService<IOptions<MicrosoftFoundryOptions>>().Value;

            IChatClient chatClient = OpenAIChatClientFactory
                .CreateClient(foundry)
                .GetChatClient(foundry.Model)
                .AsIChatClient();

            return OpenAIChatClientFactory.Decorate(chatClient, provider);
        });

        return services;
    }
}
