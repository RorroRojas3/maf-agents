# HelloAgent

The smallest useful Microsoft Agent Framework program: create an agent, ask it a question, and get the answer back two different ways.

## What it demonstrates

- Building an `AIAgent` from an `OpenAIClient` via the Responses client
- `RunAsync` — await the complete response
- `RunStreamingAsync` — consume `AgentResponseUpdate` values as the model produces them
- Reading configuration from user-secrets or the environment so no key is ever committed

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- An [OpenAI API key](https://platform.openai.com/api-keys)

## Run it

From the repository root:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/01-get-started/HelloAgent
dotnet run --project samples/01-get-started/HelloAgent
```

Setting `OPENAI_API_KEY` in your environment works instead of user-secrets. Without either — or with the variable set to an empty string, which is what an unpopulated CI secret looks like — the sample stops and tells you which command to run.

Ctrl+C cancels an in-flight request and exits with code `130`.

## Configuration

| Key | Environment variable | Default | Purpose |
| --- | --- | --- | --- |
| `OpenAI:ApiKey` | `OPENAI_API_KEY` | *(required)* | Your OpenAI API key |
| `OpenAI:Model` | `OPENAI_MODEL` | `gpt-4o-mini` | Model the agent runs on |

The double-underscore forms (`OpenAI__ApiKey`, `OpenAI__Model`) work too — that is the standard .NET environment-variable spelling for a nested configuration key.

## Expected output

```
--- Run (complete response) ---
Paris is the largest city in France.

--- RunStreaming (token by token) ---
France is the most visited country in the world.
```

The second call streams, so you see it arrive a few characters at a time rather than all at once.

## Notes

**No memory between calls.** Neither run is passed an `AgentSession`, so each one starts a fresh conversation — the agent remembers nothing from the first question when it answers the second. That is why the second prompt names France again instead of saying "it". Carrying context across turns is what `AgentSession` is for, and it gets its own sample.

**`OPENAI001`.** `OpenAIClient.GetResponsesClient()` is still marked evaluation-only by the OpenAI SDK, so the call site is wrapped in `#pragma warning disable OPENAI001`. The suppression is deliberately scoped to those few lines rather than set repo-wide, so a future sample that never touches the Responses API does not silently inherit it. The Responses API is the client Agent Framework's documentation recommends and the one carrying the full hosted-tool surface, which is why the samples use it despite the attribute.
