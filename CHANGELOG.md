# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

This change set repurposes the repository. It began as a fork of `awesome-claude-copilot` — a dual-harness Claude Code + GitHub Copilot *configuration* repo for C#/.NET and Angular, with no application source of its own — and is now a home for runnable Microsoft Agent Framework agent examples in C#. Earlier commits on this branch cleared out what no longer applied: the Angular/NgRx skills, the `.github/` Copilot tree, `.vscode/`, the previous `docs/`, `scripts/repo-audit.mjs`, both slash commands, and five of the six rules.

### Added

- **First runnable sample — [`samples/01-get-started/HelloAgent`](samples/01-get-started/HelloAgent/).** Builds an `AIAgent` on the OpenAI Responses client and answers a question two ways: `RunAsync` for the complete response and `RunStreamingAsync` for a streamed one. It reads the API key from `dotnet user-secrets` or `OPENAI_API_KEY` — nothing sensitive is committed — and exits with code `130` on Ctrl+C. The sample folder carries its own README covering configuration, expected output, and the concepts it does *not* show.
- **Solution scaffolding**, so a new sample needs only a project file and a solution entry: `maf-agents.slnx`, `Directory.Build.props` (net10.0, latest C#, nullable, warnings-as-errors, XML documentation generation), `Directory.Packages.props` (central package management), `global.json`, `.editorconfig`, `.gitattributes`, and `.gitignore`.
- **Code style enforced by the build.** Formatting (IDE0055), naming (IDE1006), unused usings (IDE0005), and missing XML docs on public APIs (CS1591) now fail the build rather than waiting for someone to remember `dotnet format`.
- **An architecture decision record for the model provider**, [`docs/adr/0001-openai-as-model-provider.md`](docs/adr/0001-openai-as-model-provider.md), recording why the samples call OpenAI directly instead of Microsoft Foundry or Azure OpenAI, and the conditions under which that should be revisited.

### Changed

- **`.claude/CLAUDE.md` rewritten for this repo**: the sample layout, the stable-packages-only constraint, OpenAI as the provider, and explicit carve-outs for API-key handling and for samples not being unit-tested — deviations by decision, so they are not "fixed" by a later reviewer.
- **`README.md` rewritten.** It still described `awesome-claude-copilot`; it now tells you how to clone the repo, supply a key, and run a sample.
- **`microsoft-agent-framework` skill refreshed.** `references/dotnet.md` was rewritten against the current .NET surface and now includes a table of which packages are GA and which ship prerelease only. `SKILL.md` no longer claims the whole framework is in public preview, since the core packages are GA at 1.17.0.
- **`.claude/settings.json` and `.mcp.json` trimmed** to the servers and plugins this repo uses — the `angular-cli` and `terraform` MCP entries and four unused plugins are gone.

### Removed

- The three `github-actions-*` skills. Their reviewer agent no longer exists, and there are no workflows here for them to act on.
