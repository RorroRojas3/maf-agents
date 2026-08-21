# ImageAgent

An agent whose whole job is images. It carries three tools — read an image, draw one, redraw one — and decides for itself which to reach for.

## What it demonstrates

- An `AIAgent` with several `AIFunction` tools, where the model does the routing
- Reaching **Microsoft Foundry** through its OpenAI-compatible `/openai/v1/` route with API-key auth, using only GA packages
- Two deployments in one agent: a chat model that thinks and reads images, an image model that draws them
- `AgentSession`, so "now make that one a night scene" resolves against the image from the previous turn
- Keeping large binary results **out of** the model's context while losing none of the data
- Reading credentials from the shared, git-ignored `config/appsettings.json`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- A Microsoft Foundry resource with **two** deployments:
  - a chat model that supports tool calling and image input — `gpt-5.6-luna` by default
  - an image model — `gpt-image-2` by default

Both are needed. `gpt-image-2` only emits images: it cannot call tools and it cannot produce text, so it cannot drive the agent or read an image back.

## Run it

From the repository root:

```bash
cp config/appsettings.sample.json config/appsettings.json
# fill in Foundry:Endpoint and Foundry:ApiKey
dotnet run --project samples/02-agents/ImageAgent
```

`config/appsettings.json` is git-ignored and shared by every sample, so the endpoint and key are entered once. Prefer to keep the key out of any file? Use user-secrets instead:

```bash
dotnet user-secrets set "Foundry:ApiKey" "<key>" --project samples/02-agents/ImageAgent
```

Then talk to it:

```
> draw a watercolour fox sitting under a pine tree
> what is in that image?
> now make it a night scene with a full moon
```

`/new` starts a fresh session, `/output` lists everything written this run, `/help` repeats the commands, `/exit` quits. Exit codes follow `sysexits.h`: `0` normal, `69` the service could not be reached or refused the request, `78` a configuration problem, `130` cancelled with Ctrl+C.

## Configuration

| Key | Environment variable | Default | Purpose |
| --- | --- | --- | --- |
| `Foundry:Endpoint` | `FOUNDRY_ENDPOINT`, `AZURE_OPENAI_ENDPOINT` | *(required)* | Resource endpoint, ending in `/openai/v1/` |
| `Foundry:ApiKey` | `FOUNDRY_API_KEY`, `AZURE_OPENAI_API_KEY` | *(required)* | Foundry API key |
| `Foundry:ChatModel` | `FOUNDRY_CHAT_MODEL` | `gpt-5.6-luna` | Deployment that drives the agent and reads images |
| `Foundry:ImageModel` | `FOUNDRY_IMAGE_MODEL` | `gpt-image-2` | Deployment that draws and edits images |
| `ImageAgent:OutputDirectory` | — | `output` | Root for per-run artifact folders |
| `ImageAgent:DefaultImageSize` | — | `1024x1024` | Size used when a request does not name one |
| `ImageAgent:DefaultQuality` | — | `high` | Quality used when a request does not name one |
| `ImageAgent:LogLevel` | — | `Warning` | Minimum level for framework diagnostics |

Providers layer `config/appsettings.json` → user-secrets → environment variables. The `Foundry__Endpoint` double-underscore spelling is the standard .NET environment form and follows that precedence. The flat names in the table are a convenience fallback consulted only when the `Foundry:` key is blank everywhere, which is why the sample file ships `Endpoint` and `ApiKey` empty rather than pre-filled — a placeholder would shadow them.

Model names are **deployment names**, not catalogue names. The v1 route resolves what you named the deployment in your resource.

## The three tools

| Tool | Does | Backed by |
| --- | --- | --- |
| `extract_text_from_image` | Transcribes and describes an image on disk | chat model, via the Responses API |
| `generate_image_from_text` | Draws a new image | image model, `/images/generations` |
| `transform_image` | Redraws an existing image, optionally through a mask | image model, `/images/edits` |

Each validates its arguments against the documented service limits *before* calling out — size rules, format allow-lists, the 20 MB vision cap, the 50 MB edit cap, the 4 MB mask cap. A local failure names the constraint that was broken, which the model can act on; the generic HTTP 400 the service would return instead tends to get retried unchanged.

Sizes follow the `gpt-image-2` rules: both edges a multiple of 16, long edge at most 3840, aspect ratio at most 3:1, and between 655,360 and 8,294,400 total pixels. `auto` leaves the choice to the service.

## Nothing is lost

Image bytes never travel through the model's context. Base64 of a 4K image is megabytes of tokens — it would evict the conversation, and it can be truncated without anything saying so.

Instead every run gets its own folder, `output/<yyyyMMdd-HHmmss>/`, and each tool:

1. writes the full-fidelity bytes there, and
2. returns a small JSON descriptor — absolute path, media type, byte count, SHA-256, the size and quality requested, the source image for a transform, and the `RevisedPrompt` when the service rewrote the prompt it was given.

Extracted text is returned to the model **complete**, never summarized, and is also written to a `.txt` beside the images so it outlives the process. The console prints the agent's full reply followed by the paths written that turn.

File names are composed from a fixed label and a counter, never from anything the model produced, so a tool cannot be talked into writing outside the run folder. The counter is incremented atomically because this agent's model supports parallel tool calls.

Reads are deliberately *not* confined that way: `extract_text_from_image` and `transform_image` open whatever path they are given, because reading an arbitrary image on your disk is the entire point. The model chooses that argument, so treat it as untrusted — the agent can be asked to read, and describe back, any image file the process account can open. If you adapt this sample into something that serves other people, confine the read path to a directory you control.

## Notes

**Why not `Microsoft.Agents.AI.Foundry`?** It ships prerelease only, and this repository is stable-packages-only. Foundry's `/openai/v1/` route speaks the OpenAI protocol, so `OpenAIClient` with an `Endpoint` reaches it over GA packages end to end — the pattern Microsoft documents for [the v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle#code-changes).

**Why the Responses API for both calls?** The GPT-5.6 series [will not do tool calling over Chat Completions](https://learn.microsoft.com/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure#gpt-56) unless reasoning is switched off. The agent therefore uses the Responses client, and the vision call inside `extract_text_from_image` goes through the same client rather than mixing the two APIs.

**`OPENAI001`.** Three call sites are evaluation-only in `OpenAI` 2.12.0: `GetResponsesClient()`, `ImageEditOptions.Quality`, and the `LowQuality`/`MediumQuality`/`HighQuality` tiers — the ones the GPT-image models actually accept, as opposed to the older DALL·E `Standard`/`High` pair. Each suppression is scoped to its own few lines rather than set repo-wide, matching [HelloAgent](../../01-get-started/HelloAgent/README.md).

**`input_fidelity` is not used.** `ImageEditOptions.InputFidelity` exists in the SDK, but Azure documents the parameter as supported for `gpt-image-1`-series models only, and this sample targets `gpt-image-2`.

**Rate limits.** Image deployments default to 5 images/minute on Foundry. A quick succession of draw-then-redraw requests will hit it; the sample reports HTTP 429 with that context rather than a bare status code.
