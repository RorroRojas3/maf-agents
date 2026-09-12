using Andes.Agents.Api.Options;
using Andes.Agents.Common.Constants;
using Andes.Agents.Repository.Agents;
using Andes.Agents.Service.Caching;
using Microsoft.Extensions.Options;

namespace Andes.Agents.Api.Startup;

internal static class AgentCatalogBootstrapper
{
    // Usage is attributed to and priced from the catalog's model, so it must be the deployment each agent's chat client calls.
    public static async Task ValidateAgentCatalogAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        AzureOpenAIOptions azureOpenAI = app.Services.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;

        Dictionary<string, (string Deployment, string Setting)> chatClients = new(StringComparer.Ordinal)
        {
            [AgentNames.Weather] = (azureOpenAI.Model, $"{AzureOpenAIOptions.SectionName}:{nameof(AzureOpenAIOptions.Model)}"),
        };

        // Warms the cache as well, so the first session summary does not wait on the store.
        AgentCatalog catalog = await app.Services.GetRequiredService<IAgentCatalogCache>().GetAsync(cancellationToken);

        foreach (string agentName in AgentNames.All)
        {
            ActiveAgentModel model = catalog.Find(agentName)
                ?? throw new InvalidOperationException($"[Core.Ref].[AgentModelMapping] has no active model for agent '{agentName}'.");

            if (!chatClients.TryGetValue(agentName, out (string Deployment, string Setting) chatClient))
            {
                throw new InvalidOperationException($"No chat client deployment is registered for agent '{agentName}'.");
            }

            if (!string.Equals(model.DeploymentName, chatClient.Deployment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The agent catalog runs '{agentName}' on deployment '{model.DeploymentName}', "
                    + $"but '{chatClient.Setting}' is '{chatClient.Deployment}'.");
            }
        }
    }
}
