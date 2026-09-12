---
paths:
  - "**/*.cs"
  - "**/*.csproj"
---

# API architecture

<!-- Portable: this file names no repository. Copy it into any .NET solution shaped Api → Service → Repository → Entity and it applies as written. Record a solution's own folder map, sanctioned deviations and adopted names in that repository's project memory, not here. -->

Where a file goes and what it is called, for a .NET minimal-API solution shaped `<Root>.Api → <Root>.Service → <Root>.Repository → <Root>.Entity`, plus `<Root>.Dto` and `<Root>.Common`, tested by `<Root>.Unit.Test` and `<Root>.Integration.Test`.

Placeholders: `<Root>` = solution prefix (`Contoso.Shop`); `<Feature>` = plural noun (`Orders`, `Documents`); `<Entity>` = singular (`Order`); `<Provider>` = a persistence technology (`Sql`, `Mongo`, `Blob`); `<Technique>` = gerund or mass noun for a sub-pipeline a feature owns (`Extraction`, `Tokenization`); `<Area>` = an options section (`Export`, `Telemetry`); `<Prefix>` = one fixed product word used on infrastructure DI methods (`AddContoso…`).

## Layering

- Project references: `Api → Service → Repository → Entity → Common`, `Dto → Common`, and `Service → Dto`. `Entity` and `Repository` never reference `Dto`; `Common` references no project.
- **The composition root declares what it names.** `Api` carries a `ProjectReference` to every project whose types appear in its source — commonly `Service`, `Repository` and `Dto` — because it registers them. Transitive flow is never relied on to make a type compile.
- No `Abstractions` project, no controllers, no AutoMapper. Interfaces live with their implementations; mapping is hand-written static classes.
- **A `Service` type is never named after a storage technology.** `<Provider>` words appear only inside `Repository/<Provider>/`. A service that persists through `I<Entity>Repository` is named for what it does (`PersistedCartStore`), not for the store behind it (`RedisCartStore`).

## Rules that apply everywhere

- **Namespace == path under `RootNamespace`.** `<Root>.Service/Documents/Extraction/X.cs` declares `namespace <Root>.Service.Documents.Extraction;`. Moving a file changes its namespace and nothing else. `Program.cs` declares no namespace. Enforce it: `dotnet_style_namespace_match_folder = true` plus `dotnet_diagnostic.IDE0130.severity = warning`, which is severity-less by default and would otherwise let a moved file build clean.
- **No loose `.cs` at a project root.** Every type is inside a folder.
- **Grouped model files per leaf folder** `<Folder>/`: non-service classes → `<Folder>Classes.cs`; records → `<Folder>Records.cs`; structs and record structs → `<Folder>Structs.cs`; const-only static holders → `<Folder>Constants.cs`; exceptions → `<Folder>Exceptions.cs`. A static class with method bodies keeps its own file. Nested and private types stay nested. Enums never live here.
- **Interfaces.** One implementation → same file as the implementation, file named after the implementation, interface declared first. Two or more implementations, or implementations that live in a subfolder → `<Feature>/Interfaces/I<Name>.cs`, one per file. A nested namespace sees its parent, so an interface in `<Feature>/Interfaces/` needs no `using` for types in `<Feature>/` — adding one is an unnecessary-using error.
- **Options.** `<Area>Options` classes, each with `public const string SectionName`. **Every project that reads options owns an `Options/` folder**, and the class lives in the lowest project that reads it — `Api/Options/` for host concerns, `Service/Options/` for business knobs, `Repository/<Provider>/Options/` for store settings. A project with provider folders puts each provider's options inside that provider's own `Options/`, never in a shared one, so a second store carries its configuration with it. Bind with `AddOptions<T>().Bind(configuration.GetSection(T.SectionName)).ValidateWithFluentValidation().ValidateOnStart()`. Never `Configure<T>`, never `*Settings`. Inside any namespace that contains an `Options` segment the folder shadows Microsoft's `Options` class: write `Microsoft.Extensions.Options.Options.Create(...)`.
- **Validation is FluentValidation**, never DataAnnotations — EF Core mapping attributes on an entity are model metadata, not validation (see `<Root>.Entity`). One `AbstractValidator<T>` per validated type, declared **in the same file as the type it validates** — the one sanctioned exception to file-name-equals-type-name, alongside the interface rule. A validator is `internal` unless another project registers it. Cross-field rules go in the validator, not in a `.Validate(lambda, message)` call on the options builder.
- **Enums** all live in `Common/Enums/`, one per file, named in the plural (`OrderStatuses`, not `OrderStatus`) so they never collide with an entity. A wire-name companion is `<Enum>Names` in the owning Service feature.
- **Constants** (string and Guid catalogs, const-only) all live in `Common/Constants/`. Dto, Entity, Service and Api hold no enums and no catalogs. A validation limit both a DTO validator and a store configuration must agree on is a catalog in `Common/Constants/`, not a constant on either.
- **Exceptions.** Every project that throws owns an `Exceptions/` folder or a `<Folder>Exceptions.cs` in the feature that throws. Only `NotFoundException` and `ForbiddenException` are solution-wide (`Service/Exceptions/`). **A provider exception never reaches Api untranslated**: `Repository/<Provider>/` throws a store-shaped exception, and the Service feature catches it and rethrows the domain exception the handler maps. Api's exception handler therefore names no Repository type, and swapping the store changes nothing above Service.
- **Tests mirror source.** `tests/<Root>.Unit.Test/<ProjectShortName>/<Folder>/<Type>Tests.cs`, ProjectShortName ∈ {Api, Service, Repository, Entity, Dto, Common}. Integration tests are grouped by feature folder plus `Endpoints/`, `Health/`, `Middleware/`. `TestInfrastructure/` at each test project root holds every fixture, fake, builder and collection definition — no helper types beside tests, no `*Tests` class inside it. A behaviour-named file (`<Behaviour>Tests.cs`) is allowed only when there is no single subject type. The test stack itself is fixed in `csharp.md`.
- **Shipped assets move with their code.** The csproj item and the `AppContext.BaseDirectory` constant that reads it change in the same commit. The output path is a deployment contract: use `Link` to keep it when the source folder moves. Glob a directory of like files (`Skills\**\*.md`, `Prompts\*.md`); enumerate a single file.

## Project layout

### `<Root>.Api`

```
<Root>.Api/
├─ Program.cs                       top-level statements; sequences Add…/Map…/Use… calls, registers nothing itself
├─ appsettings.json  appsettings.sample.json
├─ Configuration/                   all dependency injection
│  ├─ <Feature>Configuration.cs     internal static; Add<Feature>(this IServiceCollection[, IConfiguration]) — plural, so it never collides with an EF <Entity>Configuration
│  ├─ <Concern>Configuration.cs     Add<Prefix><Concern> for Authentication, Cors, KeyVault, DataProtection, ExceptionHandling…
│  ├─ ConfigurationRecords.cs       records the registrations share
│  └─ Providers/                    one <Provider>ProviderConfiguration.cs (+ <Provider>Defaults.cs) per external model or API provider
├─ Endpoints/                       minimal-API modules
│  └─ <Entity>Endpoints.cs          Map<Entity>Endpoints(this IEndpointRouteBuilder); internal static handlers
├─ ExceptionHandlers/               <Name>ExceptionHandler.cs, one IExceptionHandler per file
├─ Filters/                         <Name>EndpointFilter.cs — static factories (Require(...)), not IEndpointFilter types
├─ Health/                          <Name>HealthCheck.cs, probes, HealthRegistration.cs — the policy (names, tags, routes) even when a probe lives with its provider
├─ Middleware/                      <Name>Middleware.cs + <Name>Registration.cs + LoggerMessage partials
├─ Observability/                   telemetry enrichers and processors + TelemetryRegistration.cs
├─ Options/                         <Area>Options.cs read only by Api (Telemetry, RequestLogging, KeyVault), each with its validator
├─ Problems/                        ProblemTypes.cs (wire contract), ProblemsStructs.cs, ProblemDetailsRegistration.cs
├─ Properties/                      launchSettings.json
└─ Startup/                         <Name>Bootstrapper.cs — validators and bootstrappers that run after Build()
```

Persistence is registered by calling the provider's own `Add<Prefix><Provider>Persistence` from `Program.cs`; `Api/Configuration/` holds no store wiring of its own.

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
│  ├─ <Feature>Exceptions.cs        every exception only this feature throws, including the ones it translates store faults into
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
├─ Options/                         <Area>Options.cs, each with public const string SectionName and its validator
├─ Prompts/                         PromptTemplateLoader.cs + shipped *.md templates
├─ Security/                        caller identity, secret protection
├─ Serialization/                   converters and serializer settings
└─ Sorting/                         shared sort-key resolution; SortingStructs.cs
```

### `<Root>.Repository`

Provider-first: store-agnostic contracts sit at the top, and everything one technology needs sits in its own folder, so a second store is a sibling rather than a refactor.

```
<Root>.Repository/
├─ <Feature>/                       store-agnostic; nothing here names a provider
│  ├─ Interfaces/I<Entity>Repository.cs   one per file — the implementations live in a provider subfolder
│  ├─ <Feature>Records.cs           read/result shapes the contract exposes (e.g. an entity plus its concurrency token)
│  └─ <Feature>Exceptions.cs        store-shaped exceptions the contract documents, thrown by every provider
└─ <Provider>/                      Sql/, Mongo/, Blob/ — everything one technology needs
   ├─ <Provider>PersistenceConfiguration.cs   public static Add<Prefix><Provider>Persistence(IServiceCollection, IConfiguration)
   ├─ <Provider>Queries.cs  <Provider>Records.cs  <Provider>Containers.cs   client, connection and query plumbing
   ├─ HealthChecks/<Provider>HealthCheck.cs   internal; exposed through Add<Prefix><Provider>HealthCheck(IHealthChecksBuilder, …)
   ├─ Options/<Provider>DbOptions.cs          + its validator, in the same file
   ├─ Provisioning/                 <Provider>ResourceProvisioner.cs, <Provider>SchemaMigrator.cs — create or migrate the store when an Api/Startup bootstrapper asks
   ├─ Serialization/                converters and serializer settings this store needs
   ├─ DbContexts/  Configurations/  Migrations/   EF providers only: <RootShort>DbContext.cs, IEntityTypeConfiguration<T> mirroring the Entity folders (relationships, indexes, constraints, conversions, seed data), `dotnet ef migrations add` output
   ├─ Interceptors/                 EF providers only: <Name>Interceptor.cs, one per file — save-changes, command and connection interceptors
   └─ <Feature>/<Provider><Entity>Repository.cs   the implementation, named for the store it talks to
```

The provider folder owns its own DI, so the composition root sequences one call per store and holds no client construction. A health check and an interceptor are `internal` and attached inside the provider's `Add<Prefix><Provider>…` extensions, so the composition root never names either type. A provisioning type is `public` because an `Api/Startup/` bootstrapper resolves it: the bootstrapper decides *whether* to run, the provider type knows *how*, so Api never makes a store-specific call. A non-EF store simply has no `DbContexts/`, `Configurations/`, `Interceptors/` or `Migrations/`.

### `<Root>.Entity`

```
<Root>.Entity/
├─ Base/                            BaseEntity.cs and the abstract Base<Thing>.cs shared by aggregates
└─ <Feature>/                       one file per entity: <Entity>.cs, <Entity><Child>.cs; <Feature>Records/Constants.cs for non-relational documents
```

An entity declares its own column shape with attributes — `[Table("<Entity>", Schema = "<Schema>")]`, `[Key]`, `[DatabaseGenerated]`, `[Timestamp]`, `[Keyless]`, `[StringLength]`, `[Precision]` — so `Entity` takes `Microsoft.EntityFrameworkCore.Abstractions` and nothing else from EF Core. A string is always `nvarchar`: `[StringLength]`, never `[MaxLength]`, `[Unicode(false)]` or a fixed length. Relationships, indexes, check constraints, value conversions and seed data stay in `Repository/<Provider>/Configurations/`.

### `<Root>.Dto`

```
<Root>.Dto/
├─ Actions/<Feature>/               folder is the plural of the file prefix
│  └─ <Entity>Actions.cs            Create/Update/Delete<Entity>ActionDto, List<Entities>ActionDto, and their FluentValidation validators
├─ <Feature>/                       response DTOs, one type family per file
│  └─ <Entity>Dto.cs  <Entity><Child>Dto.cs
└─ Pagination/                      PaginatedResponseDto.cs and other shapes every feature shares
```

`List<Entities>ActionDto` is the query-parameter shape, bound with `[AsParameters]`. ASP.NET Core's built-in minimal-API validation (`AddValidation()`) reads **DataAnnotations only**, so in a FluentValidation solution it has nothing to act on and must not be registered — the endpoint attaches the validation filter instead. An endpoint that declares a validation response without attaching the filter is advertising a 400 it can never return.

### `<Root>.Common`

```
<Root>.Common/
├─ Constants/                       <Catalog>.cs — const / static readonly string and Guid catalogs, no methods
├─ Enums/                           <Enums>.cs — one enum per file, plural name
├─ Extensions/                      <Type>Extensions.cs — extension methods on BCL or Common types
└─ Validation/                      the FluentValidation-to-IValidateOptions adapter and its OptionsBuilder extension
```

`Validation/` sits here because every layer that registers options needs it and `Common` is the only project all of them share. It is the one place `Common` takes package references.

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
└─ TestInfrastructure/              WebApplicationFactory subclass, auth handler, container fixtures, seeds
```

## Decision table — "You are adding…"

| You are adding… | It goes in… | Named… |
|---|---|---|
| an endpoint module | `Api/Endpoints/` | `<Entity>Endpoints.cs`, `Map<Entity>Endpoints` |
| a DI registration for a feature | `Api/Configuration/` | `<Feature>Configuration.cs`, `Add<Feature>(this IServiceCollection, IConfiguration)`; drop the `IConfiguration` parameter when the feature binds no options |
| a DI registration for infrastructure | `Api/Configuration/`, or the owning `Api/{Health,Middleware,Observability,Problems}/` | `<Concern>Configuration.cs` / `<Concern>Registration.cs`, `Add<Prefix><Concern>` |
| a per-provider registration for a model or external API | `Api/Configuration/Providers/` | `<Provider>ProviderConfiguration.cs`, `Add<Provider>Provider` |
| **a persistence provider** | `Repository/<Provider>/` | the technology's proper name — `Sql/`, `Mongo/` |
| **a persistence registration** | `Repository/<Provider>/` | `<Provider>PersistenceConfiguration.cs`, `Add<Prefix><Provider>Persistence` |
| **a store health probe** | `Repository/<Provider>/HealthChecks/` | `<Provider>HealthCheck.cs`, internal, plus `Add<Prefix><Provider>HealthCheck`; the name, tag and route stay in `Api/Health/` |
| **a store provisioner or schema migrator** | `Repository/<Provider>/Provisioning/` | `<Provider>ResourceProvisioner.cs` / `<Provider>SchemaMigrator.cs`, public; run by an `Api/Startup/<Name>Bootstrapper.cs` |
| an options class read only by Api | `Api/Options/` | `<Area>Options.cs` with `SectionName` + validator |
| an options class read by Service | `Service/Options/` | `<Area>Options.cs` with `SectionName` + validator |
| **an options class read by a store** | `Repository/<Provider>/Options/` | `<Provider>DbOptions.cs` with `SectionName` + validator |
| **a validator** | the file of the type it validates | `<Type>Validator : AbstractValidator<<Type>>` |
| a service | `Service/<Feature>/` | `<Entity>Service.cs` (`I<Entity>Service` first) |
| an interface with one implementation | the implementation's file | `I<Impl>` above `<Impl>` |
| an interface with 2+ implementations, or one whose implementations live in a subfolder | `<Feature>/Interfaces/` | `I<Name>.cs` |
| a mapper | `Service/<Feature>/` | `<Entity>Mapper.cs`, static, `MapTo<Entity>Dto` + `MapTo<Entity>DtoExpression` |
| a request DTO or its validator | `Dto/Actions/<Feature>/` | `<Entity>Actions.cs`; `Create<Entity>ActionDto` + `Create<Entity>ActionDtoValidator`; `List<Entities>ActionDto` for query parameters |
| a response DTO | `Dto/<Feature>/` | `<Entity>Dto.cs` |
| an entity | `Entity/<Feature>/` | `<Entity>.cs` |
| a repository contract | `Repository/<Feature>/Interfaces/` | `I<Entity>Repository.cs` |
| a repository implementation | `Repository/<Provider>/<Feature>/` | `<Provider><Entity>Repository.cs` |
| an EF configuration | `Repository/<Provider>/Configurations/<Feature>/` | `<Entity>Configuration.cs` |
| a migration | `Repository/<Provider>/Migrations/` | `dotnet ef migrations add <Verb><Subject>` |
| **an EF interceptor** | `Repository/<Provider>/Interceptors/` | `<Name>Interceptor.cs`, internal, one per file; attached in `<Provider>PersistenceConfiguration.cs` |
| an enum | `Common/Enums/` | `<Enums>.cs`, plural |
| an enum's wire names | `Service/<Feature>/` | `<Enum>Names.cs`, static |
| a constant catalog | `Common/Constants/` | `<Catalog>.cs` (`PermissionIds`, `TelemetryNames`) |
| a record / struct / non-service class | the leaf folder's grouped file | `<Folder>Records.cs` / `<Folder>Structs.cs` / `<Folder>Classes.cs` |
| a feature exception | `Service/<Feature>/` (or its `<Technique>/`) | `<Folder>Exceptions.cs` |
| a store exception | `Repository/<Feature>/` | `<Folder>Exceptions.cs`; Service translates it before it reaches Api |
| a solution-wide exception | `Service/Exceptions/` | only `NotFoundException`, `ForbiddenException` |
| a static helper with method bodies | beside its callers | own file, named for what it does (`<Subject>Sql.cs`, `<Subject>Calculator.cs`) |
| a background job | `Service/BackgroundJobs/` (shared) or `Service/<Feature>/` (feature-owned) | `<Subject>Processor.cs`, `<Subject>Queue.cs` |
| a cache | `Service/Caching/` | `<Subject>Cache.cs` with `I<Subject>Cache`; `<Subject>CacheOptions` in `Service/Options/` |
| a shipped asset (prompt, template, font) | beside the code that reads it | kebab-case file; csproj `<None>` + `Link`; `AppContext.BaseDirectory` constant |
| a middleware | `Api/Middleware/` | `<Name>Middleware.cs` + `<Name>Registration.cs` |
| an endpoint filter | `Api/Filters/` | `<Name>EndpointFilter.cs`, static `Require(...)` factory |
| an exception handler | `Api/ExceptionHandlers/` | `<Name>ExceptionHandler.cs` |
| a health check | `Api/Health/` (or the owning `Repository/<Provider>/HealthChecks/`) | `<Name>HealthCheck.cs`; registered in `HealthRegistration.cs` |
| a startup validator | `Api/Startup/` | `<Name>Bootstrapper.cs` |
| a unit test | `tests/<Root>.Unit.Test/<ProjectShortName>/<Folder>/` | `<Type>Tests.cs` |
| an integration test | `tests/<Root>.Integration.Test/<Feature>/` or `Endpoints/` | `<Subject>IntegrationTests.cs` |
| test infrastructure | `tests/<Root>.*.Test/TestInfrastructure/` | `<Subject>Fixture.cs`, `Fake<Name>.cs` implementing `I<Name>`, `<Name>Collection.cs` |

## Naming table

| Thing | Convention | Examples |
|---|---|---|
| Projects | `<Root>.<Layer>`; tests `<Root>.Unit.Test`, `<Root>.Integration.Test` | `Contoso.Shop.Service` |
| Folders — collections of like types | plural | `Endpoints`, `Enums`, `Options`, `Configurations`, `HealthChecks`, `Interceptors`, `<Feature>` |
| Folders — techniques and infrastructure | gerund or mass noun | `Extraction`, `Caching`, `Middleware`, `Health`, `Observability`, `Configuration`, `Provisioning`, `Validation` |
| Folders — persistence providers | the technology's proper name | `Sql`, `Mongo`, `Blob` |
| File names | == the type name, except grouped files, and a validator beside the type it validates | `OrderService.cs`; `OrdersRecords.cs`; `ExportOptions.cs` holding `ExportOptionsValidator` |
| Grouped files | `<Folder>Classes/Records/Structs/Constants/Exceptions.cs` | `ExtractionRecords.cs` |
| Services | `<Entity>Service` / `I<Entity>Service` | `OrderService` |
| Repositories | contract `I<Entity>Repository`; implementation `<Provider><Entity>Repository` | `IOrderRepository` / `SqlOrderRepository` |
| Mappers | `<Entity>Mapper`, static | `OrderMapper` |
| Options | `<Area>Options`, `SectionName` | `ExportOptions` |
| Validators | `<Type>Validator` | `ExportOptionsValidator`, `CreateOrderActionDtoValidator` |
| DI extension classes (Api) | `<Feature>Configuration` (plural) | `OrdersConfiguration` vs EF `OrderConfiguration` |
| DI extension methods | `Add<Feature>` for features; `Add<Prefix><Thing>` for infrastructure; `Add<Prefix><Provider><Concern>` for a store | `AddOrders`; `Add<Prefix>Cors`; `AddContosoSqlPersistence` |
| `*Registration` | reserved for `Api/{Health,Middleware,Observability,Problems}` | `TelemetryRegistration` |
| Endpoint modules | `<Entity>Endpoints`, `Map<Entity>Endpoints` | `OrderEndpoints` |
| Filters | `<Name>EndpointFilter`, static `Require(...)` | `PermissionEndpointFilter` |
| Exception handlers | `<Name>ExceptionHandler` | `ValidationExceptionHandler` |
| Health checks | `<Name>HealthCheck` | `DatabaseHealthCheck` |
| EF interceptors | `<Name>Interceptor`, named for what it does | `AuditTimestampInterceptor`, `SoftDeleteInterceptor` |
| An injected `DbContext` | field `_ctx` from primary-constructor parameter `ctx`, whatever the context type — never `_dbContext` or `_context` | `private readonly OrdersDbContext _ctx = ctx;` |
| Store provisioning | `<Provider>ResourceProvisioner` creates resources; `<Provider>SchemaMigrator` applies migrations | `MongoResourceProvisioner`, `SqlSchemaMigrator` |
| Startup | `<Name>Bootstrapper` | `SchemaBootstrapper` |
| Enums | plural, one per file | `JobStatuses`, `SortDirections` |
| Enum wire names | `<Enum>Names` | `JobStatusNames` |
| Actions | `Actions/<Feature>/<Entity>Actions.cs` | `Actions/Orders/OrderActions.cs` |
| Exceptions | `<Condition>Exception`; grouped in `<Folder>Exceptions.cs` | `StorageNotConfiguredException` |
| Tests | `<Type>Tests`; integration `<Subject>IntegrationTests` | `OrderServiceTests` |
| Fakes | `Fake<Name>` implementing `I<Name>` | `FakeGraphService` |
| Shipped assets | kebab-case | `order-summary-prompt.md` |

## Never

- No loose `.cs` at a project root; no `Models/`, `Helpers/`, `Utils/`, `Tool/`, `Settings/` or `Mappers/` folders in Service; no `Exceptions/` folder in Service beyond the two shared types.
- No controllers, no `MapControllers()`, no AutoMapper, no `Abstractions` project, no `*Settings` classes, no `Configure<T>`.
- **No `Swashbuckle.AspNetCore`, `AddSwaggerGen`, `UseSwagger` or `UseSwaggerUI`** — the document comes from `Microsoft.AspNetCore.OpenApi` and the UI from Scalar.
- **No `System.ComponentModel.DataAnnotations` validation** — no `[Required]`, `[Range]`, `[Url]`, no `ValidateDataAnnotations()`. Mapping attributes on an entity are not validation.
- No constants class for column lengths or precision, and no model-owned (`HasData`) seed for rows operators change after release — write those as `InsertData` in the migration that creates the table.
- No enums or constant catalogs in Dto, Entity, Service or Api — they live in Common.
- No provider name on a type outside `Repository/<Provider>/`; no provider-specific code in Service or Api, including client construction in the composition root.
- No shared `Options/` or `Serialization/` folder at the Repository root when provider folders exist — each provider owns its own.
- No interface-only file for a 1:1 pair; no file named after the interface when it also holds the implementation.
- No test helper types outside `TestInfrastructure/`; no `*Tests` class inside it.
- Never regenerate a migration to absorb a CLR rename; edit the type-name strings and verify with `dotnet ef migrations has-pending-model-changes`.
