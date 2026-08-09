# August 2026 Standards Refresh

**Date**: 2026-08-01
**Claude Code harness**: `.claude/rules/csharp.md`, `.claude/rules/blazor-wasm.md` (renamed from `blazor.md`), `.claude/CLAUDE.md`, `.claude/agents/csharp-code-reviewer.md`
**GitHub Copilot harness**: `.github/instructions/csharp.instructions.md`, `.github/instructions/blazor-wasm.instructions.md` (renamed from `blazor.instructions.md`), `.github/copilot-instructions.md`, `.github/agents/csharp-code-reviewer.agent.md`

This refresh modernizes the always-on C# standards and rewrites the Blazor rule for standalone Blazor WebAssembly on .NET 10 — identically in **both harnesses** (Claude Code under `.claude/`, GitHub Copilot under `.github/`) — and documents how to keep this repo's MCP servers auto-approved under Claude Code's workspace trust gate.

The rule files remain the source of truth — this page explains why each change was made and where the details live. Do not copy guidance from here into code reviews; read the linked rule instead.

## Why this refresh happened

The standards exist to steer model output, and two of them were steering it backwards:

- The C# rules mandated plain `camelCase` private fields and never mentioned primary constructors or collection expressions, so Sonnet and Opus kept producing pre-C#-12 style code (`private readonly IUserRepository repository;` assigned in a hand-written constructor, `new List<T>()`, `Array.Empty<T>()`).
- The Blazor rule was written for a C# 13-era world of mixed server/WASM hosting. It recommended patterns that are wrong for a standalone WebAssembly app — some of them (the hosted template, `IJSUnmarshalledRuntime`, `blazor.boot.json`) no longer exist at all on .NET 10.

Separately, Claude Code v2.1.196 tightened how project-level settings can approve MCP servers, which silently left this repo's servers stuck at "Pending approval" in untrusted folders. That behavior needed documenting so it stops looking like a broken `.mcp.json`.

## 1. Modern C# defaults

**Where**: `.claude/rules/csharp.md` (new "Modern C# Constructs" section), summarized in `.claude/CLAUDE.md`, enforced by `.claude/agents/csharp-code-reviewer.md` (new "Modern C# constructs" review category). Mirrored identically in the Copilot harness: `.github/instructions/csharp.instructions.md`, `.github/copilot-instructions.md`, and `.github/agents/csharp-code-reviewer.agent.md`.

Three defaults changed:

- **Private fields are `_camelCase`** (e.g. `_userService`). Locals and parameters stay `camelCase`; everything else is unchanged.
- **Primary constructors are preferred** for classes such as services, controllers, and handlers. Every injected dependency is captured into a `private readonly` `_camelCase` field, and method bodies use the field — never the primary-constructor parameter:

  ```csharp
  public class UserService(IUserRepository repository, ILogger<UserService> logger) : IUserService
  {
      private readonly IUserRepository _repository = repository;
      private readonly ILogger<UserService> _logger = logger;
  }
  ```

- **Collection expressions are preferred** wherever the target type is clear: `[]`, `[1, 2, 3]`, and spreads like `[.. items, extra]` replace `new List<T>()`, `new T[] { }`, and `Array.Empty<T>()`.

The `csharp-code-reviewer` in both harnesses now flags the old forms, so existing code touched during a change will get nudged toward the new style during review.

## 2. Blazor rule: standalone Blazor WebAssembly on .NET 10

**Where**: `.claude/rules/blazor-wasm.md` and its Copilot counterpart `.github/instructions/blazor-wasm.instructions.md` (full rewrite). Both files were **renamed** from `blazor.md` / `blazor.instructions.md` to make the WASM-only scope explicit, with references updated in `.claude/CLAUDE.md`, `.claude/agents/csharp-code-reviewer.md`, `.github/copilot-instructions.md`, `.github/agents/csharp-code-reviewer.agent.md`, and `README.md`. The rules-table rows now read "Blazor WebAssembly (standalone)" instead of "Blazor components", and the files still auto-apply to `**/*.razor`, `**/*.razor.cs`, and `**/*.razor.css`.

The rule now targets one hosting model — standalone Blazor WebAssembly (`blazorwasm` template) on .NET 10 / C# 14 — and is grounded in the official Microsoft Learn docs (`learn.microsoft.com/aspnet/core/blazor`, `?view=aspnetcore-10.0`). It covers:

- **Hosting scope** — the "ASP.NET Core Hosted" template was removed in .NET 8, and standalone WASM has no render modes; the backend is called as an ordinary web API.
- **Rendering performance** — `Virtualize`, correct `@key` placement, `ShouldRender`, the `EventUtil` pattern, and the ASP0006 sequence-number analyzer.
- **State** — DI state-container services; a hard note that `ProtectedLocalStorage`/`ProtectedSessionStorage` are server-side only.
- **HTTP** — the Fetch-based `HttpClient`, and the .NET 10 change that makes response streaming the default (synchronous stream reads now throw).
- **Auth** — PKCE authorization code flow only; MSAL for Microsoft Entra ID.
- **JS interop** — ES-module isolation, `[JSImport]`/`[JSExport]` for high-frequency interop.
- **Size and startup** — AOT trade-offs, lazy-loading `.wasm` assemblies, and the .NET 10 removal of `blazor.boot.json` and `BlazorCacheBootResources`.
- **PWA, forms, and testing** — `updateViaCache: 'none'` worker registration, the .NET 10 `AddValidation()`/`[ValidatableType]` opt-in, and bUnit + Playwright.

The rule closes with an explicit **"Outdated patterns — never suggest these"** list so models cannot fall back on training-data habits (hosted template, render modes in standalone apps, implicit grant flow, `.dll` lazy-load names, and so on). If you are reviewing or writing Blazor code, read that list first.

## 3. MCP server auto-approval and the trust gate

**Where**: `.claude/CLAUDE.md`, "MCP servers" section (new trust-gate note).

Since Claude Code v2.1.196, a checked-in `.claude/settings.json` cannot approve its own repository's MCP servers while the folder is **untrusted**: the setting is ignored and the servers sit at "Pending approval" until you accept the workspace trust dialog. This is a deliberate security gate, not a configuration bug.

To keep this repo's servers loading everywhere without per-repo prompts, the recommended setup is a name-based allowlist in your **user-level** settings (`~/.claude/settings.json`):

```json
{
  "enabledMcpjsonServers": ["microsoft-learn", "terraform", "angular-cli", "context7"]
}
```

Because the list matches servers by name, it applies across every repo that defines them in its `.mcp.json`. If a server shows **Rejected** instead of pending, a stale per-project choice is cached — run `claude mcp reset-project-choices` inside that repo to clear it.

## Practical impact

- New C# written in this repo's style will look different from most existing samples: primary constructors, `_`-prefixed fields, and collection expressions are now the reviewed-for default.
- Blazor guidance no longer applies to Blazor Server or Blazor Web Apps. If a project needs those hosting models, the `blazor-wasm` rule files must be forked or extended — do not reuse the WASM rule as-is.
- No action is needed in this repo for MCP; the user-level settings change happens on each contributor's machine.
