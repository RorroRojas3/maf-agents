---
paths:
  - "**/*.cs"
---

# C# Development

## Instructions

- Always use the latest version C#, currently C# 14 features.
- Make only high confidence suggestions when reviewing code changes.
- Write maintainable code; handle edge cases with clear exception handling.
- Comment only what code cannot say: why-decisions, constraints, non-obvious invariants. Never restate what the code does or narrate a change.
- Keep a comment to one or two lines; three needs a reason, and past six the content belongs in `docs/`. Write for an engineer who knows C# — never explain the language, the framework, or an API.
- Never name a PRD story, epic, requirement id, or design frame in source (`US-1402`, `FR-11`, "frame `2f`"). Traceability belongs in the PR description and `docs/`. Full standard: `.claude/CLAUDE.md` §"Communication & comments".
- Web/API-specific standards (auth, validation, versioning, performance, deployment) live in `aspnet-rest-apis.md`.
- Where a file goes, what it is called, and which project owns it live in `api-architecture.md`.

## Naming Conventions

- Follow PascalCase for component names, method names, and public members.
- Prefix private fields with an underscore and use `_camelCase` (e.g., `_userService`, `_cache`).
- Use camelCase for local variables and parameters.
- Prefix interface names with "I" (e.g., IUserService).

## Modern C# Constructs

- Prefer primary constructors for classes (services, controllers, handlers). Capture every injected dependency into a `private readonly` `_camelCase` field at the top of the class, and reference the field — not the primary-constructor parameter — in method bodies:

  ```csharp
  public class UserService(IUserRepository repository, ILogger<UserService> logger) : IUserService
  {
      private readonly IUserRepository _repository = repository;
      private readonly ILogger<UserService> _logger = logger;
  }
  ```

- Prefer collection expressions wherever the target type is clear: `[]` for empty collections, `[1, 2, 3]` for literals, and spreads like `[.. items, extra]`. Do **not** write `new List<T>()`, `new T[] { }`, or `Array.Empty<T>()` when a collection expression works:

  ```csharp
  private readonly List<string> _names = [];
  string[] merged = [.. first, .. second];
  ```

## Choosing a type kind

Default to a record; reach for a class when the type has behaviour or identity, and a struct only when the value is small and immutable.

- **`sealed record`** — the default for data: DTOs, documents, results, and any shape passed between layers. Value equality and `with` are what you want, and positional syntax keeps it to one line.
- **`sealed class`** — behaviour and identity: services, middleware, converters, handlers. Also **options classes**, because the configuration binder needs settable properties and a parameterless constructor, and **exceptions**, which must derive from `Exception`.
- **`readonly record struct`** — small immutable values that are allocated often: ids, keys, coordinates, a private result shape. Rule of thumb: at most three fields, no reference-heavy payload, never boxed (do not implement an interface you will call through), and not held in a field of a long-lived async state machine, where copying costs more than the allocation saved.
- **plain `struct`** — interop and explicit layout only.
- Seal by default. `abstract` only for a deliberate base type; never a non-sealed plain `class`.

## Formatting

- Apply code-formatting style defined in `.editorconfig`.
- Prefer file-scoped namespace declarations and single-line using directives.
- Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
- Ensure that the final return statement of a method is on its own line.
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Give public APIs a one-sentence `<summary>`; implementations use `/// <inheritdoc />`. Add `<param>`, `<returns>`, `<remarks>`, or `<example>` only where they carry what the signature does not.
- `<remarks>` is a caveat a caller must know, two sentences at most — not rationale, history, or alternatives weighed. `internal` and test types are not API surface: document them only where a *why* exists.
- Where this conflicts with the `csharp-docs` skill, this wins: the skill describes .NET's framework-reference house style, not this codebase's.

## Project Structure

- Create projects from the appropriate .NET templates.
- Organize code with feature folders or domain-driven design; keep models, services, and data access in separate layers.
- Use the .NET configuration system with environment-specific settings.

## Nullable Reference Types

- Declare variables non-nullable, and check for `null` at entry points.
- Always use `is null` or `is not null` instead of `== null` or `!= null`.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## Validation

- Validate with **FluentValidation**. Never `System.ComponentModel.DataAnnotations` — no `[Required]`, `[Range]`, `[Url]`, no `ValidateDataAnnotations()`. EF Core mapping attributes on an entity (`[Table]`, `[Key]`, `[DatabaseGenerated]`, `[Timestamp]`, `[Keyless]`, `[StringLength]`, `[Precision]`) are model metadata, not validation — see Data Access.
- One `AbstractValidator<T>` per validated type, in the same file as the type it validates.
- Options are validated the same way: `AddOptions<T>().Bind(...).ValidateWithFluentValidation().ValidateOnStart()`, with the matching `IValidator<T>` registered alongside. Put a cross-field rule in the validator, not in a `.Validate(lambda, message)` call on the builder.
- Chain `.Cascade(CascadeMode.Stop)` before a rule whose predicate would throw on the value the previous rule rejects.

## Data Access

- Use Entity Framework Core where a relational store and change tracking earn it; a document or key-value store reached through its own SDK is equally valid, and `ef-core` guidance then does not apply.
- Apply the repository pattern where it adds value.
- With EF Core, manage schema with migrations; seed data where needed.
- **EF Core mapping is hybrid.** The entity declares its column shape with attributes: `[Table("<Entity>", Schema = "<Schema>")]`, `[Key]`, `[DatabaseGenerated]`, `[Timestamp]`, `[Keyless]`, `[StringLength]`, `[Precision]`. A string is always `nvarchar`: `[StringLength]`, never `[MaxLength]`, `[Unicode(false)]` or a fixed length. Its `IEntityTypeConfiguration<T>` holds the rest: relationships, indexes, check constraints, value conversions (with a converted enum's length, since `[StringLength]` on a non-string throws under DataAnnotations validation) and seed data. Column lengths and precision live in those attributes, never in a constants class.
- **LINQ is method syntax only** — `.Where(...).Join(...).Select(...)`, never query syntax (`from … in … select`), whether against EF Core or in memory.
- Write efficient queries — avoid N+1 and over-fetching.

## Logging and Monitoring

- Use structured logging (e.g. Serilog) with appropriate log levels.
- Integrate Application Insights; correlate requests with correlation IDs.
- Monitor performance, errors, and usage.

## Testing

- Always include test cases for critical paths of the application.
- **xUnit v3**, methods named `MethodName_Scenario_ExpectedBehavior`, Arrange-Act-Assert structure.
- Do not emit "Act", "Arrange" or "Assert" comments.
- Never name a PRD story or epic in a test name, comment, or `DisplayName`; the name states the behaviour under test.
- Copy existing style in nearby files for test method names and capitalization.
- **Isolate with NSubstitute. Never Moq** — its 4.20 releases shipped a dependency that harvested developer email addresses, and the trust cost is not worth re-litigating per project.
- **Plain xUnit `Assert`. Never FluentAssertions** — v8 and later require a paid licence for commercial use.
- Integration tests run against the real dependency in **Testcontainers**; where no image exists, a local test instance is the fallback. Never assert against an in-memory substitute for a database that ships as a container.
- Code whose only behaviour is a call to a live model is not unit-tested.
- This section **overrides the `csharp-xunit` skill**, which suggests Moq and a fluent assertion library.
