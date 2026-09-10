using Andes.Agents.Api.Configuration;
using Andes.Agents.Api.Configuration.Providers;
using Andes.Agents.Api.Endpoints;
using Andes.Agents.Api.Health;
using Andes.Agents.Api.Middleware;
using Andes.Agents.Api.Observability;
using Andes.Agents.Api.Problems;
using Andes.Agents.Api.Startup;
using Andes.Agents.Repository.Cosmos;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAndesKeyVault();

builder.Services.AddAndesForwardedHeaders();
builder.Services.AddAndesRequestLogging(builder.Configuration);
builder.Services.AddAndesTelemetry(builder.Configuration);
builder.Services.AddAndesAuthentication(builder.Configuration);
builder.Services.AddAndesCors(builder.Configuration);
builder.Services.AddAndesRateLimiting(builder.Configuration);
builder.Services.AddAndesProblemDetails();
builder.Services.AddAndesExceptionHandling();
builder.Services.AddAndesHealthChecks();
builder.Services.AddAndesOpenApi(builder.Configuration);
builder.Services.AddAndesAzureCredential(builder.Configuration);
builder.Services.AddAndesCosmosPersistence(builder.Configuration);
builder.Services.AddCoreServices();
builder.Services.AddWeather();
builder.Services.AddMicrosoftFoundryProvider(builder.Configuration);
builder.Services.AddAzureOpenAIProvider(builder.Configuration);
builder.Services.AddSessions(builder.Configuration);
builder.Services.AddAgents(builder.Configuration);

WebApplication app = builder.Build();

app.UseForwardedHeaders();
app.UseAndesRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapAndesOpenApi();
app.MapAndesHealthChecks();
app.MapAgentEndpoints();
app.MapSessionEndpoints();

// Validation otherwise runs with the host, which is after provisioning: nothing external should be touched
// by a process that is about to fail on its own configuration.
app.Services.GetRequiredService<IStartupValidator>().Validate();

await app.EnsureCosmosResourcesAsync();
await app.RunAsync();
