# maf-agents

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Agents built with the [Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/) in C#.

## Status

Early, and worth stating plainly: this branch holds one solution — [weather-agent/Andes.Agents/](weather-agent/Andes.Agents/) — scaffolded from `dotnet new webapi` and not yet implemented.

- **No Agent Framework package is referenced anywhere yet.** There is no agent, model client, or tool.
- Four of the five projects (`Common`, `Entity`, `Repository`, `Service`) are empty class libraries — a `.csproj` each and no source.
- `Andes.Agents.Api` has no `Controllers/` directory, so it starts and maps no routes.

An earlier version of this repository held runnable console samples (`HelloAgent`, `ImageAgent`), two architecture decision records, and central package management. That content lives on the `main` branch and is not present here; `git show main:<path>` reads any of it.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

There is no `global.json`, so the SDK version is not pinned.

## Layout

```
maf-agents/
├── .claude/                        Claude Code project memory, rules, skills, subagents
├── weather-agent/Andes.Agents/
│   ├── Andes.Agents.slnx           the only solution file
│   ├── Andes.Agents.Api/           ASP.NET Core web API (scaffold)
│   ├── Andes.Agents.Common/        empty
│   ├── Andes.Agents.Entity/        empty
│   ├── Andes.Agents.Repository/    empty
│   └── Andes.Agents.Service/       empty
├── .editorconfig                   C# style, enforced by dotnet format
├── .gitattributes                  LF line endings
├── CHANGELOG.md                    empty
└── LICENSE
```

The project names imply the layering `Api → Service → Repository → Entity`, plus `Common`. No project references are wired between them yet.

## Build and run

```bash
git clone https://github.com/RorroRojas3/maf-agents.git
cd maf-agents

dotnet build weather-agent/Andes.Agents/Andes.Agents.slnx
dotnet run --project weather-agent/Andes.Agents/Andes.Agents.Api
```

There is no solution file at the repository root, so every command names its path.

Under the Development environment the API serves its OpenAPI document at `/openapi/v1.json`. Nothing else is mapped.

## Conventions

**Style** lives in [.editorconfig](.editorconfig), which encodes the C# standards in [.claude/rules/csharp.md](.claude/rules/csharp.md) — file-scoped namespaces, `_camelCase` private fields, `I`-prefixed interfaces, PascalCase members, LF endings, 4-space C# indentation. Because this branch has no `Directory.Build.props`, the **build does not enforce any of it**; `dotnet format` is the check:

```bash
dotnet format weather-agent/Andes.Agents/Andes.Agents.slnx --verify-no-changes
```

Both `dotnet build` and that check pass on the current tree.

**Package versions** are declared inline in each `.csproj`. Central package management is used on `main`, but no `Directory.Packages.props` exists here.

**Line endings** are normalised to LF by [.gitattributes](.gitattributes). Without it, a Windows clone with `core.autocrlf=true` fails the format check on every file.

**No secrets are committed.** When a model provider is wired up, supply credentials through `dotnet user-secrets` or environment variables — never a tracked file.

**No CI.** There are no GitHub Actions workflows; the build and format checks run locally only.

## Adding a project

1. Create it under `weather-agent/Andes.Agents/` and add it to `Andes.Agents.slnx`.
2. Declare `net10.0`, `Nullable` and `ImplicitUsings` in the `.csproj` to match the existing projects, and give every `PackageReference` an explicit `Version`.
3. Confirm `dotnet build` and `dotnet format --verify-no-changes` both pass.

## License

[MIT](LICENSE)
