---
paths:
  - "weather-agent/**"
---

# API architecture

<!-- Portable: the body uses placeholders only. To adopt in another repository, copy this file, change `paths:` above, and replace the "This repository" appendix at the end. -->

Where a file goes and what it is called, for a .NET minimal-API solution shaped `<Root>.Api → <Root>.Service → <Root>.Repository → <Root>.Entity`, plus `<Root>.Dto` and `<Root>.Common`, tested by `<Root>.Unit.Test` and `<Root>.Integration.Test`.

Placeholders: `<Root>` = solution prefix (`Contoso.Shop`); `<Feature>` = plural noun (`Orders`, `Documents`); `<Entity>` = singular (`Order`); `<Technique>` = gerund or mass noun for a sub-pipeline a feature owns (`Extraction`, `Tokenization`); `<Area>` = an options section (`Export`, `Telemetry`); `<Prefix>` = one fixed product word used on infrastructure DI methods (`AddContoso…`).

## Layering

- Project references: `Api → Service → Repository → Entity → Common`, `Dto → Common`, and `Repository → Dto` (EF configurations read validation rule constants from the action DTOs); `Entity` never references `Dto`; `Common` references nothing.
- No `Abstractions` project, no controllers, no AutoMapper. Interfaces live with their implementations; mapping is hand-written static classes.

## Rules that apply everywhere

- **Namespace == path under `RootNamespace`.** `<Root>.Service/Documents/Extraction/X.cs` declares `namespace <Root>.Service.Documents.Extraction;`. Moving a file changes its namespace and nothing else. `Program.cs` declares no namespace.
- **No loose `.cs` at a project root.** Every type is inside a folder.
- **Grouped model files per leaf folder** `<Folder>/`: non-service classes → `<Folder>Classes.cs`; records → `<Folder>Records.cs`; structs and record structs → `<Folder>Structs.cs`; const-only static holders → `<Folder>Constants.cs`; exceptions → `<Folder>Exceptions.cs`. A static class with method bodies keeps its own file. Nested and private types stay nested. Enums never live here.
- **Interfaces.** One implementation → same file as the implementation, file named after the implementation, interface declared first. Two or more implementations, or implementations that live in a subfolder → `<Feature>/Interfaces/I<Name>.cs`, one per file.
- **Options.** `<Area>Options` classes, each with `public const string SectionName`. Read by Service → `Service/Options/`; read only by Api → `Api/Options/`. Registered in `Api/Configuration` with `AddOptions<T>().Bind(configuration.GetSection(T.SectionName)).Validate(...).ValidateOnStart()`. Never `Configure<T>`, never `*Settings`. Inside any namespace that contains an `Options` segment the folder shadows Microsoft's `Options` class: write `Microsoft.Extensions.Options.Options.Create(...)`.
- **Enums** all live in `Common/Enums/`, one per file, named in the plural (`OrderStatuses`, not `OrderStatus`) so they never collide with an entity. A wire-name companion is `<Enum>Names` in the owning Service feature.
- **Constants** (string and Guid catalogs, const-only) all live in `Common/Constants/`. Dto, Entity, Service and Api hold no enums and no catalogs.
- **Exceptions.** Only `NotFoundException` and `ForbiddenException` are solution-wide (`Service/Exceptions/`). Every other exception goes in the `<Folder>Exceptions.cs` of the feature that throws it.
- **Tests mirror source.** `tests/<Root>.Unit.Test/<ProjectShortName>/<Folder>/<Type>Tests.cs`, ProjectShortName ∈ {Api, Service, Repository, Entity, Dto, Common}. Integration tests are grouped by feature folder plus `Endpoints/`, `Health/`, `Middleware/`. `TestInfrastructure/` at each test project root holds every fixture, fake, builder and collection definition — no helper types beside tests, no `*Tests` class inside it. A behaviour-named file (`<Behaviour>Tests.cs`) is allowed only when there is no single subject type.
- **Shipped assets move with their code.** The csproj item and the `AppContext.BaseDirectory` constant that reads it change in the same commit. The output path is a deployment contract: use `Link` to keep it when the source folder moves. Glob a directory of like files (`Skills\**\*.md`, `Prompts\*.md`); enumerate a single file.

## Project layout

### `<Root>.Api`

```
<Root>.Api/
├─ Program.cs                       top-level statements; sequences Add…/Map…/Use… calls, registers nothing itself
├─ appsettings.json  appsettings.sample.json
├─ Configuration/                   all dependency injection
│  ├─ <Feature>Configuration.cs     internal static; Add<Feature>(this IServiceCollection[, IConfiguration]) — plural, so it never collides with an EF <Entity>Configuration
│  ├─ <Concern>Configuration.cs     Add<Prefix><Concern> for Authentication, Cors, Persistence, KeyVault, DataProtection, ExceptionHandling…
│  ├─ ConfigurationRecords.cs       records the registrations share
│  └─ Providers/                    one <Provider>ProviderConfiguration.cs (+ <Provider>Defaults.cs) per external provider
├─ Endpoints/                       minimal-API modules
│  └─ <Entity>Endpoints.cs          Map<Entity>Endpoints(this IEndpointRouteBuilder); internal static handlers
├─ ExceptionHandlers/               <Name>ExceptionHandler.cs, one IExceptionHandler per file
├─ Filters/                         <Name>EndpointFilter.cs — static factories (Require(...)), not IEndpointFilter types
├─ Health/                          <Name>HealthCheck.cs, probes, HealthRegistration.cs
├─ Middleware/                      <Name>Middleware.cs + <Name>Registration.cs + LoggerMessage partials
├─ Observability/                   telemetry enrichers and processors + TelemetryRegistration.cs
├─ Options/                         <Area>Options.cs read only by Api (Telemetry, RequestLogging, KeyVault)
├─ Problems/                        ProblemTypes.cs (wire contract), ProblemsStructs.cs, ProblemDetailsRegistration.cs
├─ Properties/                      launchSettings.json
└─ Startup/                         <Name>Bootstrapper.cs — validators and bootstrappers that run after Build()
```

### `<Root>.Service`

```
<Root>.Service/
├─ <Feature>/                       one folder per feature, mirroring Api/Endpoints
│  ├─ <Entity>Service.cs            I<Entity>Service + <Entity>Service in one file, interface first
│  ├─ <Entity>Mapper.cs             static MapTo<Entity>Dto + Expression<Func<<Entity>, <Entity>Dto>> projection
│  ├─ <Enum>Names.cs                wire-name companion of a Common enum this feature owns
│  ├─ <Feature>Classes.cs           every non-service top-level class of this folder
│  ├─ <Feature>Records.cs           every top-level record of this folder
│  ├─ <Feature>Structs.cs           every struct and record struct of this folder
│  ├─ <Feature>Constants.cs         const-only static holders of this folder
│  ├─ <Feature>Exceptions.cs        every exception only this feature throws
│  ├─ Interfaces/                   only when an interface has 2+ implementations or they live in a subfolder
│  │  └─ I<Name>.cs
│  └─ <Technique>/                  a sub-pipeline the feature owns; the same file rules apply recursively
│     ├─ <Variant><Technique-agent>.cs   e.g. <Format>TextExtractor.cs, <Format>ExportRenderer.cs
│     ├─ <Technique>Records.cs  <Technique>Structs.cs  <Technique>Exceptions.cs
│     └─ <Assets>/                  shipped files the technique reads (fonts, templates) + their resolver
├─ BackgroundJobs/                  queue, processor, status store; BackgroundJobsRecords/Constants/Exceptions.cs
├─ Caching/                         <Subject>Cache.cs (I<Subject>Cache in the same file); CachingClasses/Structs.cs
├─ Exceptions/                      NotFoundException.cs, ForbiddenException.cs — nothing else
├─ Observability/                   metrics and tracing statics
├─ Options/                         <Area>Options.cs, each with public const string SectionName
├─ Prompts/                         PromptTemplateLoader.cs + shipped *.md templates
├─ Security/                        caller identity, secret protection
├─ Serialization/                   converters and serializer settings
└─ Sorting/                         shared sort-key resolution; SortingStructs.cs
```

### `<Root>.Repository`

```
<Root>.Repository/
├─ DbContexts/                      <RootShort>DbContext.cs; OnModelCreating applies every configuration explicitly
├─ Configurations/                  IEntityTypeConfiguration<T>, mirroring the Entity folders
│  └─ <Feature>/<Entity>Configuration.cs
└─ Migrations/                      dotnet ef migrations add only; hand-edit nothing but CLR-name strings after a type move
```

### `<Root>.Entity`

```
<Root>.Entity/
├─ Base/                            BaseEntity.cs and the abstract Base<Thing>.cs shared by aggregates
└─ <Feature>/                       one file per entity: <Entity>.cs, <Entity><Child>.cs; <Feature>Records/Constants.cs for non-relational documents
```

### `<Root>.Dto`

```
<Root>.Dto/
├─ Actions/<Feature>/               folder is the plural of the file prefix
│  └─ <Entity>Actions.cs            Create/Update/Delete<Entity>ActionDto + their FluentValidation validators (+ rule holders with methods)
├─ <Feature>/                       response DTOs, one type family per file
│  └─ <Entity>Dto.cs  <Entity><Child>Dto.cs
└─ Pagination/                      PaginatedResponseDto.cs and other shapes every feature shares
```

### `<Root>.Common`

```
<Root>.Common/
├─ Constants/                       <Catalog>.cs — const / static readonly string and Guid catalogs, no methods
├─ Enums/                           <Enums>.cs — one enum per file, plural name
└─ Extensions/                      <Type>Extensions.cs — extension methods on BCL or Common types
```

### Tests

```
tests/<Root>.Unit.Test/
├─ Api/<Folder>/<Type>Tests.cs      mirrors <Root>.Api
├─ Service/<Feature>/<Technique>/<Type>Tests.cs
├─ Repository/  Entity/  Dto/  Common/
└─ TestInfrastructure/              fixtures, fakes, builders, collection definitions, KnownIds.cs

tests/<Root>.Integration.Test/
├─ Endpoints/<Entity>EndpointsIntegrationTests.cs
├─ <Feature>/<Subject>IntegrationTests.cs
├─ Health/  Middleware/             infrastructure behaviour through the real pipeline
└─ TestInfrastructure/              WebApplicationFactory subclass, auth handler, container fixtures, fakes, seeds
```

## Decision table — "You are adding…"

| You are adding… | It goes in… | Named… |
|---|---|---|
| an endpoint module | `Api/Endpoints/` | `<Entity>Endpoints.cs`, `Map<Entity>Endpoints` |
| a DI registration for a feature | `Api/Configuration/` | `<Feature>Configuration.cs`, `Add<Feature>(this IServiceCollection, IConfiguration)`; drop the `IConfiguration` parameter when the feature binds no options |
| a DI registration for infrastructure | `Api/Configuration/`, or the owning `Api/{Health,Middleware,Observability,Problems}/` | `<Concern>Configuration.cs` / `<Concern>Registration.cs`, `Add<Prefix><Concern>` |
| a per-provider registration | `Api/Configuration/Providers/` | `<Provider>ProviderConfiguration.cs`, `Add<Provider>Provider` |
| an options class read by Service | `Service/Options/` | `<Area>Options.cs` with `SectionName` |
| an options class read only by Api | `Api/Options/` | `<Area>Options.cs` with `SectionName` |
| a service | `Service/<Feature>/` | `<Entity>Service.cs` (`I<Entity>Service` first) |
| an interface with one implementation | the implementation's file | `I<Impl>` above `<Impl>` |
| an interface with 2+ implementations | `Service/<Feature>/Interfaces/` | `I<Name>.cs` |
| a mapper | `Service/<Feature>/` | `<Entity>Mapper.cs`, static, `MapTo<Entity>Dto` + `MapTo<Entity>DtoExpression` |
| a request DTO or its validator | `Dto/Actions/<Feature>/` | `<Entity>Actions.cs`; `Create<Entity>ActionDto`, `Create<Entity>ActionDtoValidator` |
| a response DTO | `Dto/<Feature>/` | `<Entity>Dto.cs` |
| an entity | `Entity/<Feature>/` | `<Entity>.cs` |
| an EF configuration | `Repository/Configurations/<Feature>/` | `<Entity>Configuration.cs` |
| a migration | `Repository/Migrations/` | `dotnet ef migrations add <Verb><Subject>` |
| an enum | `Common/Enums/` | `<Enums>.cs`, plural |
| an enum's wire names | `Service/<Feature>/` | `<Enum>Names.cs`, static |
| a constant catalog | `Common/Constants/` | `<Catalog>.cs` (`PermissionIds`, `TelemetryNames`) |
| a record / struct / non-service class | the leaf folder's grouped file | `<Folder>Records.cs` / `<Folder>Structs.cs` / `<Folder>Classes.cs` |
| a feature exception | `Service/<Feature>/` (or its `<Technique>/`) | `<Folder>Exceptions.cs` |
| a solution-wide exception | `Service/Exceptions/` | only `NotFoundException`, `ForbiddenException` |
| a static helper with method bodies | beside its callers | own file, named for what it does (`<Subject>Sql.cs`, `<Subject>Calculator.cs`) |
| a background job | `Service/BackgroundJobs/` (shared) or `Service/<Feature>/` (feature-owned) | `<Subject>Processor.cs`, `<Subject>Queue.cs` |
| a cache | `Service/Caching/` | `<Subject>Cache.cs` with `I<Subject>Cache`; `<Subject>CacheOptions` in `Service/Options/` |
| a shipped asset (prompt, template, font) | beside the code that reads it | kebab-case file; csproj `<None>` + `Link`; `AppContext.BaseDirectory` constant |
| a middleware | `Api/Middleware/` | `<Name>Middleware.cs` + `<Name>Registration.cs` |
| an exception handler | `Api/ExceptionHandlers/` | `<Name>ExceptionHandler.cs` |
| a health check | `Api/Health/` | `<Name>HealthCheck.cs`; registered in `HealthRegistration.cs` |
| a startup validator | `Api/Startup/` | `<Name>Bootstrapper.cs` |
| a unit test | `tests/<Root>.Unit.Test/<ProjectShortName>/<Folder>/` | `<Type>Tests.cs` |
| an integration test | `tests/<Root>.Integration.Test/<Feature>/` or `Endpoints/` | `<Subject>IntegrationTests.cs` |
| test infrastructure | `tests/<Root>.*.Test/TestInfrastructure/` | `<Subject>Fixture.cs`, `Fake<Name>.cs` implementing `I<Name>`, `<Name>Collection.cs` |

## Naming table

| Thing | Convention | Examples |
|---|---|---|
| Projects | `<Root>.<Layer>`; tests `<Root>.Unit.Test`, `<Root>.Integration.Test` | `Contoso.Shop.Service` |
| Folders — collections of like types | plural | `Endpoints`, `Enums`, `Options`, `Configurations`, `<Feature>` |
| Folders — techniques and infrastructure | gerund or mass noun | `Extraction`, `Caching`, `Middleware`, `Health`, `Observability`, `Configuration` |
| File names | == the type name, except grouped files | `OrderService.cs`; `OrdersRecords.cs` |
| Grouped files | `<Folder>Classes/Records/Structs/Constants/Exceptions.cs` | `ExtractionRecords.cs` |
| Services | `<Entity>Service` / `I<Entity>Service` | `OrderService` |
| Mappers | `<Entity>Mapper`, static | `OrderMapper` |
| Options | `<Area>Options`, `SectionName` | `ExportOptions` |
| DI extension classes (Api) | `<Feature>Configuration` (plural) | `OrdersConfiguration` vs EF `OrderConfiguration` |
| DI extension methods | `Add<Feature>` for features; `Add<Prefix><Thing>` for infrastructure | `AddOrders`; `Add<Prefix>Cors` |
| `*Registration` | reserved for `Api/{Health,Middleware,Observability,Problems}` | `TelemetryRegistration` |
| Endpoint modules | `<Entity>Endpoints`, `Map<Entity>Endpoints` | `OrderEndpoints` |
| Filters | `<Name>EndpointFilter`, static `Require(...)` | `PermissionEndpointFilter` |
| Exception handlers | `<Name>ExceptionHandler` | `ValidationExceptionHandler` |
| Health checks | `<Name>HealthCheck` | `DatabaseHealthCheck` |
| Startup | `<Name>Bootstrapper` | `SchemaBootstrapper` |
| Enums | plural, one per file | `JobStatuses`, `SortDirections` |
| Enum wire names | `<Enum>Names` | `JobStatusNames` |
| Actions | `Actions/<Feature>/<Entity>Actions.cs` | `Actions/Orders/OrderActions.cs` |
| Exceptions | `<Condition>Exception`; grouped in `<Folder>Exceptions.cs` | `StorageNotConfiguredException` |
| Tests | `<Type>Tests`, methods `Method_Scenario_Expected`; integration `<Subject>IntegrationTests` | `OrderServiceTests` |
| Fakes | `Fake<Name>` implementing `I<Name>` | `FakeGraphService` |
| Shipped assets | kebab-case | `order-summary-prompt.md` |

## Never

- No loose `.cs` at a project root; no `Models/`, `Helpers/`, `Utils/`, `Tool/`, `Settings/` or `Mappers/` folders in Service; no `Exceptions/` folder beyond the two shared types.
- No controllers, no `MapControllers()`, no AutoMapper, no `Abstractions` project, no `*Settings` classes, no `Configure<T>`.
- No enums or constant catalogs in Dto, Entity, Service or Api — they live in Common.
- No interface-only file for a 1:1 pair; no file named after the interface when it also holds the implementation.
- No test helper types outside `TestInfrastructure/`; no `*Tests` class inside it.
- Never regenerate a migration to absorb a CLR rename; edit the type-name strings and verify with `dotnet ef migrations has-pending-model-changes`.

---

## This repository — Andes.Agents

`<Root>` = `Andes.Agents`; `<Prefix>` = `Andes` (`AddAndesKeyVault`, `AddAndesTelemetry`, `AddAndesAuthentication`, `AddAndesCors`, `AddAndesRateLimiting`, `AddAndesProblemDetails`, `AddAndesExceptionHandling`, `AddAndesHealthChecks`, `AddAndesOpenApi`, `AddAndesAzureCredential`, `AddAndesPersistence`); `AddCoreServices` registers the clock, the caller context, the prompt loader and minimal-API validation. The solution lives at `weather-agent/Andes.Agents/Andes.Agents.slnx`; `Directory.Build.props` and `Directory.Packages.props` beside it hold the shared settings and every package version.

**Folders per project**

| Project | Folders |
|---|---|
| Api | `Endpoints/{Agent,Session}Endpoints.cs` + `WeatherAgentCard.cs`; `Configuration/{Agents,Authentication,AzureCredential,CoreServices,Cors,ExceptionHandling,ForwardedHeaders,KeyVault,OpenApi,Persistence,RateLimiting,Sessions,Weather}Configuration.cs` + `AzureCredentials.cs` + `Providers/{MicrosoftFoundry,AzureOpenAI}ProviderConfiguration.cs` + `Providers/OpenAIChatClientFactory.cs`; `ExceptionHandlers/GlobalExceptionHandler.cs`; `Health/`; `Middleware/RequestLogging{Middleware,Registration}.cs`; `Observability/{TelemetryRegistration,RequestDescriptor}.cs`; `Options/{AgentCard,ApiDocs,Azure,AzureAd,AzureOpenAI,Cors,CosmosDb,KeyVault,MicrosoftFoundry,OpenAIEndpoint,RateLimiting,RequestLogging,Telemetry}Options.cs`; `Problems/{ProblemTypes,ProblemDetailsRegistration,ProblemResponseWriter}.cs`; `Startup/CosmosBootstrapper.cs` |
| Service | `Agents/UsageRecordingAgent.cs`; `Sessions/{CosmosAgentSessionStore,CosmosChatHistoryProvider,SessionService,SessionMapper,SessionIdValidator,SessionsExceptions}.cs`; `Weather/{WeatherService,WeatherRecords}.cs` + `Weather/Tools/WeatherToolProvider.cs`; cross-cutting `Exceptions`, `Options`, `Prompts`, `Security`, `Serialization` |
| Repository | `Cosmos/{CosmosContainers,CosmosQueries,CosmosRecords,CosmosResourceProvisioner,PartitionKeys}.cs`; `Sessions/{SessionRepository,SessionMessageRepository,SessionsRecords,SessionsExceptions}.cs`; `Serialization/{RepositoryJsonOptions,UtcDateTimeOffsetJsonConverter}.cs` |
| Entity | `Sessions/SessionsRecords.cs` (Cosmos documents and the state-bag records) |
| Dto | `Actions/Sessions/SessionActions.cs`; `Sessions/`; `Pagination/` |
| Common | `Constants/{AgentNames,AuthorizationPolicies,ChatClientKeys,ClaimTypeNames,PromptNames,RateLimitPolicies,SessionIdRules,SessionStateKeys,TelemetryNames,WeatherLimits}.cs` |

**Sanctioned deviations**

- **No EF Core.** The only store is Azure Cosmos DB through the raw SDK, so `Repository/` carries `Cosmos/`, `Sessions/` and `Serialization/` in place of `DbContexts/`, `Configurations/` and `Migrations/`. Options the repositories need arrive as the `CosmosContainerNames` record, registered by `AddAndesPersistence` from `Api/Options/CosmosDbOptions.cs`; `Repository/` holds no options class.
- **The prompt is an embedded resource**, not a shipped `<None>` asset: `Service/Prompts/weather-agent-instructions.md` is read by `PromptTemplateLoader` through its logical name in `Common/Constants/PromptNames.cs`.
- **Routes are wire contracts.** The `Sessions` feature is served at `api/conversations` by `SessionEndpoints`; the A2A and AG-UI hosts live in `AgentEndpoints` at `weather/a2a` and `weather/ui`, and the agent card at `/.well-known/agent-card.json`.
- **`Api/Options/OpenAIEndpointOptions.cs` is an abstract base, not a section.** `MicrosoftFoundryOptions` and `AzureOpenAIOptions` derive from it and carry the `SectionName`; the base holds the endpoint contract both providers validate.
- **One exception handler, not one per condition.** `ExceptionHandlers/GlobalExceptionHandler.cs` maps every exception in a single switch expression, so adding a domain exception means adding an arm there and a type to `Problems/ProblemTypes.cs`.
- **`Problems/ProblemTypes.cs` stays in Api** as the rule's own layout lists it; every other constant catalog is in `Common/Constants/`.

**Sanctioned names** — `Session` is the domain word for a conversation (`SessionDocument`, `SessionService`, `SessionEndpoints`); `Conversation` appears only in the route and in `ConversationBusyException`, which mirrors the `conversation-busy` problem type. `Chat` means the model client or wire role (`ChatClientKeys`, `CosmosChatHistoryProvider`).

**Tests** — none yet. When they arrive they follow the layout above under `weather-agent/Andes.Agents/tests/`.

```bash
# from the repo root
dotnet build weather-agent/Andes.Agents/Andes.Agents.slnx
dotnet format weather-agent/Andes.Agents/Andes.Agents.slnx --verify-no-changes
```
