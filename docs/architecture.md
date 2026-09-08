# Architecture

## Current

Kitchain is a **modular monolith**. Angular runs separately in development, with a single ASP.NET Core API and a single PostgreSQL database. Courts discovery is the first implemented backend slice; Angular remains at the introduction-page stage.

### Frontend

Angular 22.0.8 with CLI/build 22.0.9, TypeScript 6, standalone components, zoneless change detection, Router and SCSS. The 22.0 release line was checked against the [official Node compatibility table](https://angular.dev/reference/versions); local Node 24.19.0 is supported. Exact Angular patches are pinned and npm's lockfile records the complete dependency tree.

```text
src/app/
  core/layout/        Responsive navigation
  shared/             Small feature introduction component and entrance directive
  features/           Home, Courts, Play and Gear introduction pages
  app.*               Shell, route table and application configuration
```

All four pages use lazy route components. Unknown routes return to Home. Route titles identify the page; navigation exposes `aria-current`. The shell uses a skip link, semantic landmarks, visible focus, fixed mobile navigation with safe-area clearance, and readable text contrast. Tokens remain small and provisional. Fonts use local system stacks; no external font download is required.

Signal inputs supply the shared feature introduction content. There is no state store or speculative service. HttpClient will be provided when the first HTTP use case exists; the current pages do not call the API.

- **Lucide:** `@lucide/angular` 1.42.0, the [current standalone Angular package](https://lucide.dev/guide/angular/getting-started). Only the four navigation icons are imported. Icons accompany text and are hidden from assistive technology.
- **Motion:** `motion` 13.2.0 using the framework-independent `motion/mini` entry point. One 350 ms opacity/8 px entrance per page, with no layout dependency. It skips unsupported browsers and reduced-motion preferences, finishes if the preference changes, and cancels on component destruction. Content is visible without animation.
- **Three.js:** `three` 0.185.1 is installed as requested, with no imports, scenes, assets or WebGL dependency. It is excluded from the application bundle by being unused.

Vitest and jsdom use Angular's generated unit-test builder. Angular ESLint covers TypeScript and templates; Prettier handles formatting. No UI framework, additional state library, Playwright or Storybook is present.

### Backend

All five projects target .NET 8, with nullable reference types and implicit usings enabled.

```text
Kitchain.Api ──→ Kitchain.Application ──→ Kitchain.Domain
      │
      └───────→ Kitchain.Infrastructure ──→ Kitchain.Application

Kitchain.Tests ──→ Kitchain.Api
```

- **Api:** composition root, configuration, dependency injection, health checks, Problem Details exception middleware and development-only Swagger/OpenAPI.
- **Application:** `CourtDiscoveryService`, validated search input, explicit summary/detail/page DTOs and `ICourtDiscoveryReader`.
- **Domain:** independent venue aggregate, amenity lookup/join types and named enums.
- **Infrastructure:** EF Core 8.0.30, Npgsql EF provider 8.0.11, explicit Fluent configuration, migration, query implementation and opt-in development seeder. References Application and uses its transitive Domain dependency.
- **Tests:** xUnit unit tests and `WebApplicationFactory` HTTP tests. Courts integration tests opt into real PostgreSQL with isolated schemas; the original health/OpenAPI tests run without a database.

`backend/global.json` selects SDK 8.0.424 with patch roll-forward. The API uses standard ASP.NET Core configuration and reads `ConnectionStrings:Kitchain` (environment key `ConnectionStrings__Kitchain`). No credentials are present in tracked application settings.

`GET /health` reports process liveness without probing the database. `GET /health/ready` checks PostgreSQL connectivity through the DbContext and propagates cancellation. Missing configuration or a failed connection produces an unhealthy result. Startup does not open a database connection, create tables, apply migrations or seed data.

`AddProblemDetails` and `UseExceptionHandler` establish standard exception responses. Health responses intentionally expose only the aggregate status, without connection details. Swagger middleware runs only in Development and documents the two Courts read routes and string enums. Health-check middleware is verified directly via HTTP. The development profile binds to localhost:5080 over HTTP.

### Courts discovery

`CourtsController → CourtDiscoveryService → ICourtDiscoveryReader → EfCourtDiscoveryReader → PostgreSQL`.

The read interface has only the two present use cases; it keeps EF and `IQueryable` out of Application without adding generic repositories, MediatR or CQRS infrastructure. The service validates and normalizes filters, including the currency used by a price cap. ASP.NET Core API controllers use the same DataAnnotations for automatic 400 Validation Problem Details. Missing or non-public detail records return 404. No write routes exist.

The `Court` aggregate represents a venue. Its constructor enforces required bounded name/address/city, positive court count, defined enum values, paired and bounded optional coordinates, nonnegative decimal(12,2) prices, and normalized three-letter currency codes. Known prices require currency; price units require prices. Optional HTTP(S) URLs must be absolute and cannot contain embedded credentials. All statuses require the minimum useful venue information. Properties have private setters, and amenities are exposed read-only with duplicate codes removed.

`CreatedAt` and `UpdatedAt` are UTC timestamps; at creation they are equal. This read-only phase has no update operations. There is no verification timestamp: provenance describes origin (`OwnerSupplied`, `CommunitySupplied`, `KitchainCurated`), not verification or approval. Lifecycle is `Draft`, `Published`, `Inactive`; both public queries explicitly restrict to Published.

`Courts` stores the venue fields. `Amenities` contains the 13 stable string codes installed by the migration; `CourtAmenities` has a composite `(CourtId, AmenityCode)` key and foreign keys, preventing duplicate or unknown assignments. There is no comma-separated or JSON amenity field. A venue deletion cascades to its assignments; lookup deletion is restricted. Database checks reinforce required text, positive counts, coordinates, price/currency, enum values and timestamp ordering.

All reads are async, cancellable, `AsNoTracking`, and projected to contracts. Filters execute in SQL. City is an exact case-insensitive comparison, with surrounding whitespace trimmed. Amenity filtering uses EXISTS. List queries use a filtered COUNT plus one paginated projection with amenities in the same SQL query; detail uses one projection. There is no lazy-loading or per-venue query loop. An index on `(Status, Name, Id)` supports publication filtering and stable pagination; amenity foreign keys are indexed. No GIS or search infrastructure is added.

List filters combine with AND: `city`, `indoorOutdoor`, `minCourts`, `maxStartingPrice`, `currencyCode`, and one `amenity`. Indoor/Outdoor filters do not implicitly include Mixed. A price cap excludes unknown prices and defaults to PHP; explicit currency selection never converts money. Price units are descriptive metadata, not normalized rates or quotes. Pages default to 1/20, cap page size at 100 and page number at 1,000,000, and return filtered totals. Separate COUNT/data statements are not a transactional snapshot; concurrent future edits could change totals between statements.

Summary and detail contracts expose `availability: { status: "NotIntegrated", message: ... }`. This is an integration boundary, not a claim that a venue has no free courts or no online schedule. Future availability can extend this object without replacing venue identity or introducing slot columns now. Surface and opening hours are optional descriptive text, not scheduling logic.

### PostgreSQL and Docker

Root Compose runs only `postgres:18`, a [supported major release](https://www.postgresql.org/support/versioning/). The tag accepts maintenance updates within major 18. The named volume mounts `/var/lib/postgresql`, matching the image's versioned data directory layout for PostgreSQL 18. The published port binds only to loopback. An internal `pg_isready` health check gates `compose up --wait`.

Development values come from ignored `.env`, copied from `.env.example`. The API connection string is set separately in its terminal; Compose environment loading does not configure a host-run .NET process. Angular and the API are not containerized.

`AddCourtsDiscovery` is the first migration. EF CLI 8.0.30 is pinned in `backend/.config/dotnet-tools.json`; EF Design is a private tooling dependency of the startup project. Developers explicitly run `dotnet ef database update --project Kitchain.Infrastructure --startup-project Kitchain.Api` from `backend/` after `dotnet tool restore`.

Venue seeding is separate from migrations and normal startup. `dotnet run --project Kitchain.Api --launch-profile http -- --seed-courts` checks the Development environment, adds only missing fixed IDs, and exits. Seven clearly fictional Metro Manila samples span multiple cities, classifications, prices, amenities and booking methods; five Published, one Draft, one Inactive. Unknown price/coordinates stay null rather than inventing real-world facts. Lookup codes are production schema data; fictional venues are opt-in development data.

PostgreSQL tests use `KITCHAIN_TEST_CONNECTION_STRING`. They create a GUID-named test schema, configure both search path and EF migration history into it, apply the actual migration and seed, exercise the actual HTTP/EF/Npgsql path, and drop only their own schema afterward. No substitute provider or Testcontainers is used. Without the variable, tests explicitly skip that boundary. Interrupted test processes may leave an isolated test schema; normal teardown removes it.

## Planned, not implemented

Phase 2B will introduce the Courts Explore UI using these contracts. Further modules will add actual domain types and use cases incrementally.

Court-provider adapters, community moderation, SignalR sessions, queues, scoring, deterministic PaddleMatch, identity, background synchronization and analytics remain future work. There are no message brokers, caches, microservices, CQRS frameworks, deployment pipelines or production hosting configuration.
