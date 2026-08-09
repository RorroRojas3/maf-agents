# Microsoft Agent Framework for .NET

Use this reference when the target project is written in C# or another .NET language.

## Authoritative sources

- Docs: <https://learn.microsoft.com/agent-framework/>
- Repository: <https://github.com/microsoft/agent-framework/tree/main/dotnet>
- Samples: <https://github.com/microsoft/agent-framework/tree/main/dotnet/samples>
- API reference: `Microsoft.Agents.AI` namespace on learn.microsoft.com

The framework moves fast. Verify package versions and API shapes against the `microsoft-learn` MCP server or NuGet before writing code — do not rely on the snippets below being the newest form.

## Packages: GA vs prerelease

The core framework reached GA; several integrations did not. Check before adding a reference:

| Package | Status | Purpose |
| --- | --- | --- |
| `Microsoft.Agents.AI` | **GA** | Core agent abstractions, `AIAgent`, `ChatClientAgent`, `AgentSession` |
| `Microsoft.Agents.AI.Abstractions` | **GA** | Base contracts for custom agents |
| `Microsoft.Agents.AI.OpenAI` | **GA** | OpenAI + Azure OpenAI clients (`AsAIAgent` on responses/chat clients) |
| `Microsoft.Agents.AI.Workflows` | **GA** | Graph workflows, orchestrations, checkpointing |
| `Microsoft.Agents.AI.Foundry` | prerelease | Microsoft Foundry projects and service-managed agents |
| `Microsoft.Agents.AI.Hosting[.*]` | prerelease | ASP.NET Core hosting, A2A / AG-UI protocol adapters |
| `Microsoft.Agents.AI.A2A` | prerelease | Agent-to-Agent protocol client |
| `Azure.AI.Projects` | prerelease | Required by the Foundry provider |
| `Azure.AI.OpenAI` | last GA is **2.1.0** (Dec 2024) | Predates the Responses API; every later release is beta |

To check current versions rather than guessing:

```bash
dotnet package search Microsoft.Agents.AI --exact-match   # latest stable
dotnet package search Microsoft.Agents.AI --prerelease    # latest including preview
```

**If the repo forbids prerelease packages, that rules out Foundry, hosting, and A2A.** The OpenAI provider is the only path that is GA end to end — `Microsoft.Agents.AI.OpenAI` depends on `OpenAI` (GA) and not on `Azure.AI.OpenAI`. Raise the constraint with the user rather than quietly adding a preview package.

## The three capability categories

| Category | Use for |
| --- | --- |
| **Agents** | One LLM-backed actor: processes input, calls tools and MCP servers, returns a response |
| **Harness** | Opinionated agent for long multi-step tasks — planning, todo tracking, context compaction, file access, tool approval, observability |
| **Workflows** | Graph of agents and functions with type-safe routing, checkpointing, and human-in-the-loop |

Reach for an agent when the path is decided at runtime; a workflow when the topology is known up front.

## Creating an agent

Any `Microsoft.Extensions.AI.IChatClient` can back an agent via `ChatClientAgent`. Provider packages add `AsAIAgent` extensions so you rarely construct it directly.

```csharp
using Microsoft.Agents.AI;
using OpenAI;

OpenAIClient client = new(apiKey);

AIAgent agent = client.GetResponsesClient()
    .AsAIAgent(
        model: "gpt-4o-mini",
        instructions: "You are a helpful assistant.",
        name: "HelloAgent");

Console.WriteLine(await agent.RunAsync("What is the largest city in France?"));

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Tell me a fun fact."))
{
    Console.Write(update);
}
```

Prefer the **Responses** client over Chat Completions where the provider offers both — it carries the full hosted-tool surface (code interpreter, file search, web search, hosted MCP).

## Function tools

Any method becomes a tool through `AIFunctionFactory.Create`. `[Description]` on the method and its parameters is what the model reads when choosing between tools, so write it for the model, not for a human reader.

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

AIAgent agent = client.GetResponsesClient()
    .AsAIAgent(model: "gpt-4o-mini", instructions: "...", tools: [AIFunctionFactory.Create(GetWeather)]);
```

Wrap a tool in `ApprovalRequiredAIFunction` to gate it behind human approval; the response then carries `FunctionApprovalRequestContent` instead of a result. An agent itself becomes a tool for another agent via `agent.AsAIFunction()` — note the returned function is stateful and must not be shared across concurrent conversations.

## Multi-turn conversations

Without a session, every run is a fresh interaction.

```csharp
AgentSession session = await agent.CreateSessionAsync();
await agent.RunAsync("My name is Alice.", session);
var response = await agent.RunAsync("What is my name?", session);

var serialized = agent.SerializeSession(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

Service-managed sessions hold an opaque service-side ID scoped to the API key or project. If one key serves multiple end users, store those IDs server-side and authorize the caller before resuming — otherwise one user can resume another's conversation.

Context providers supply persistent memory; context compaction keeps long conversations inside the window.

## Workflows and orchestrations

Built-in orchestrations, all in `Microsoft.Agents.AI.Workflows`:

| Pattern | Use when |
| --- | --- |
| Sequential | Each agent builds on the previous one's output |
| Concurrent | Independent subtasks, fan out then aggregate |
| Handoff | Route to a specialist; interactive and multi-turn by default |
| Group chat | Debate, review, brainstorming in a shared conversation |
| Magentic | A manager agent plans and dynamically selects specialists |

```csharp
var workflow = AgentWorkflowBuilder.BuildSequential([writerAgent, reviewerAgent]);

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is RequestInfoEvent e &&
        e.Request.TryGetDataAs(out ToolApprovalRequestContent? approval))
    {
        await run.SendResponseAsync(e.Request.CreateResponse(approval.CreateResponse(approved: true)));
    }
}
```

Use `WorkflowBuilder` with `RequestPort` nodes for custom graphs that pause for external input. Sequential, concurrent, and group chat do not pause for free-form user input on their own — pair them with a `RequestPort`, or use handoff, when a human needs to steer between steps.

## Hosting (prerelease)

`Microsoft.Agents.AI.Hosting` registers agents in DI off `IHostApplicationBuilder`:

```csharp
builder.AddAIAgent("pirate", instructions: "Speak like a pirate.", chatClientServiceKey: "chat-model")
    .WithInMemorySessionStore();
```

`AddWorkflow(...).AddAsAIAgent()` exposes a workflow through the plain agent interface so A2A and OpenAI protocol adapters can consume it. The in-memory session and task stores are development-only — they lose state on restart and are not shared across instances.

## Sample taxonomy

The official `dotnet/samples` tree is organized as `01-get-started`, `02-agents`, `03-workflows`, `04-hosting`, `05-end-to-end`. Mirror those category names when adding samples so they map onto the upstream docs.

## .NET conventions

- `async`/`await` throughout; flow a `CancellationToken` into agent and workflow runs.
- Register clients and agents through DI rather than constructing them per call.
- Some APIs carry `[Experimental]` attributes that surface as build diagnostics — suppress the specific reported IDs, never a blanket suppression.
- Never hardcode an API key or endpoint. Use `DefaultAzureCredential` for Azure-backed providers; for key-based providers read from user-secrets or the environment.
