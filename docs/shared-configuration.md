# Shared sample configuration

## Overview

Every sample in this repo needs the same handful of settings — a provider endpoint, an API key, a model name — and asking a reader to re-enter them for each one they try is friction that has nothing to do with what the sample is teaching. One git-ignored file, `config/appsettings.json`, sits at the repo root and is copied into every sample's build output, so an endpoint and key are entered once and every current and future sample can read them.

Use this file for settings that are genuinely shared — provider connection details. A sample's own behavioral knobs (output directories, default sizes, log levels) still belong in their own section of the same file, keyed by the sample's name, exactly the way [`ImageAgent`](../samples/02-agents/ImageAgent/) does it — there's no reason to invent a second file for that.

## Quick start

```bash
cp config/appsettings.sample.json config/appsettings.json
# open config/appsettings.json and fill in the sections a sample needs
dotnet run --project samples/02-agents/ImageAgent
```

`config/appsettings.sample.json` is the tracked template — every key present, every value blank or defaulted. `config/appsettings.json` is your filled-in copy; it never leaves your machine. Prefer to keep a key out of any file at all? Every sample also accepts `dotnet user-secrets` and environment variables — see [Precedence](#precedence-file--user-secrets--environment) below.

## Core concepts

### One file, copied everywhere

[`Directory.Build.props`](../Directory.Build.props) links the root file into every sample's build output as `appsettings.json`, conditionally:

```xml
<ItemGroup Condition="Exists('$(MSBuildThisFileDirectory)config/appsettings.json')">
  <Content Include="$(MSBuildThisFileDirectory)config/appsettings.json"
           Link="appsettings.json"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Because `Directory.Build.props` applies to every project under the repo root, this single block is what makes the file available to `HelloAgent`, `ImageAgent`, and every sample added after them — a new sample needs no configuration wiring of its own beyond reading `appsettings.json` the normal .NET way. The `Condition` matters as much as the copy: on a fresh clone with no `config/appsettings.json` yet, the `ItemGroup` simply doesn't apply, and `dotnet build` still succeeds. Nothing breaks for a reader who hasn't configured anything yet — they just see a clear error the first time a sample actually needs a value that is missing (see [Troubleshooting](#troubleshooting)).

### Precedence: file → user-secrets → environment

Every sample layers its configuration the same way, lowest priority first:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();
```

`config/appsettings.json` is the baseline — good for anything not sensitive (endpoints, model/deployment names) and, if you're comfortable with it living unencrypted on disk, the key too. User-secrets override it and store outside the repository tree entirely, which is the better home for a key you'd rather not have sitting in a plain JSON file. Environment variables override both, which is what CI and containers use.

`ImageAgent` adds one more layer underneath all three: a set of flat fallback names (`FOUNDRY_ENDPOINT`, `FOUNDRY_API_KEY`, `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, …) consulted only when the corresponding `Foundry:` key is blank everywhere else. This is why `config/appsettings.sample.json` ships `Foundry:Endpoint` and `Foundry:ApiKey` **empty** rather than filled with a placeholder: a non-blank placeholder in the tracked file would out-rank the flat variables and shadow them, silently defeating the fallback for anyone relying on it. Blank is a deliberate default, not an oversight.

### What stays out of git

```gitignore
# Local credentials shared by the samples. The tracked sample file stays.
config/appsettings.json
config/appsettings.*.json
!config/appsettings.sample.json

# Images and transcripts the samples generate at runtime.
output/
```

`config/appsettings.json` — your filled-in copy — is ignored. So is any other `config/appsettings.*.json` you might create locally (an `appsettings.Development.json`, say). The negation on the last line keeps the tracked template, `config/appsettings.sample.json`, staged normally despite the broader pattern above it. `output/`, where samples write generated images and transcripts, is ignored too — it's runtime output, not configuration, but it lives at the repo root for the same reason the config file does, so it's grouped here.

### The dangling reference this file closes

[`maf-agents.slnx`](../maf-agents.slnx) has listed `config/appsettings.sample.json` as a solution item since it was added there — a forward reference to a file that didn't exist on disk yet. Adding the tracked file closes that gap; nothing in `maf-agents.slnx` itself needed to change.

## Adding a new sample

A new sample needs three things to participate:

1. Reference `Microsoft.Extensions.Configuration.Json` (version is already pinned in [`Directory.Packages.props`](../Directory.Packages.props) — no version attribute in the `.csproj`).
2. Build the same three-layer `ConfigurationBuilder` shown above.
3. Add a section to `config/appsettings.sample.json` named after the sample (see `ImageAgent`'s section for the pattern), with every key present and either blank or defaulted — never a real value.

Nothing else is required. `Directory.Build.props` already copies the file into the new sample's output directory the moment it's built.

## Examples

**Minimal — [`HelloAgent`](../samples/01-get-started/HelloAgent/):** one `.AddJsonFile("appsettings.json", ...)` call layered under user-secrets and the environment, reading a flat `OpenAI:ApiKey` / `OpenAI:Model` pair with `configuration["OpenAI:ApiKey"]`-style indexing — no strongly typed options class, because there's only two settings.

**Typed — [`ImageAgent`](../samples/02-agents/ImageAgent/):** a `ConfigurationLoader` that binds `Foundry:*` and `ImageAgent:*` sections onto `FoundryOptions` and `ImageAgentOptions` (via `Microsoft.Extensions.Configuration.Binder`), applies the flat-name fallback described above, and runs `Validate()` on the result — collecting every configuration problem into one actionable message instead of stopping at the first.

## Troubleshooting

**Fresh clone, no `config/appsettings.json` yet, but `dotnet build` still works.** Expected — the `ItemGroup` in `Directory.Build.props` is conditioned on the file's existence, so a clone with no local credentials still compiles. A sample that needs a value will tell you what's missing the first time you *run* it, not at build time.

**"is not configured" at startup, listing missing keys.** The sample collected every configuration problem before reporting any of them, rather than failing on the first one. Fix all of the listed keys in one pass — through `config/appsettings.json`, user-secrets, or the environment — rather than fixing one and re-running to find the next.

**Confirm your filled-in file really is invisible to git.** `git status config/` should show nothing for `config/appsettings.json` itself; `git check-ignore -v config/appsettings.json` will name the `.gitignore` rule that's excluding it.

**A setting you changed doesn't seem to apply.** Check precedence: user-secrets and environment variables both out-rank `config/appsettings.json`. `dotnet user-secrets list --project samples/<category>/<Sample>` shows anything set there for that sample.
