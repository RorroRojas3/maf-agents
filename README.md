# maf-agents

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Agent Framework](https://img.shields.io/badge/Microsoft_Agent_Framework-1.17.0-0078D4?logo=microsoft&logoColor=white)](https://learn.microsoft.com/agent-framework/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Runnable **[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/)** examples in **C#**.

Each sample is a self-contained console app that demonstrates one concept — create an agent, give it tools, keep conversation state, compose a workflow — small enough to read in a sitting and copy into your own project.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later
- An [OpenAI API key](https://platform.openai.com/api-keys)

## Run a sample

```bash
git clone https://github.com/RorroRojas3/maf-agents.git
cd maf-agents
dotnet build

# store the key outside the repo — never commit it
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project samples/01-get-started/HelloAgent

dotnet run --project samples/01-get-started/HelloAgent
```

`OPENAI_API_KEY` in your environment works as an alternative to user-secrets. Override the model per sample with `OpenAI:Model` (default `gpt-4o-mini`).

## Samples

Categories mirror the [official Agent Framework sample taxonomy](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples).

| Sample | Shows |
| --- | --- |
| [01-get-started/HelloAgent](samples/01-get-started/HelloAgent/) | Creating an agent and getting both a complete and a streamed response |

More are on the way across `02-agents` (tools, middleware, providers), `03-workflows` (sequential, concurrent, handoff, group chat, magentic), `04-hosting`, and `05-end-to-end`.

## How this repo is built

**Stable packages only.** No `--prerelease` anywhere. The Agent Framework core (`Microsoft.Agents.AI`, `.Abstractions`, `.OpenAI`, `.Workflows`) is GA at 1.17.0, so the samples build on that. This is also why they use OpenAI directly rather than Microsoft Foundry or Azure OpenAI: `Microsoft.Agents.AI.Foundry`, `Azure.AI.Projects`, and every `Azure.AI.OpenAI` release after 2.1.0 ship prerelease only. The full reasoning, and what would justify changing it, is in [ADR-0001](docs/adr/0001-openai-as-model-provider.md).

**Central package management.** Every version lives in [Directory.Packages.props](Directory.Packages.props); no `.csproj` carries a `Version` attribute. Shared compiler settings — `net10.0`, latest C#, nullable, warnings-as-errors — live in [Directory.Build.props](Directory.Build.props).

**No secrets, ever.** Samples read the API key from `dotnet user-secrets` (stored outside the repo tree) or the environment. Nothing sensitive is committed.

## Contributing a sample

1. Create the project under `samples/<NN-category>/<SampleName>/` and add it to `maf-agents.slnx`.
2. Reference packages without versions; add any new version to `Directory.Packages.props`.
3. Include a `README.md` in the sample folder covering what it demonstrates, prerequisites, and how to run it.
4. Confirm `dotnet build` and `dotnet format --verify-no-changes` both pass, then add a row to the table above.

## License

[MIT](LICENSE)
