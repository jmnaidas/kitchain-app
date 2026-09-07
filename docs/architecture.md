# Architecture

## Current

Kitchain starts as a **modular monolith foundation**. Angular runs separately in development, with a single ASP.NET Core API and a single PostgreSQL database. No product modules or domain models have been implemented yet.

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
      └───────→ Kitchain.Infrastructure

Kitchain.Tests ──→ Kitchain.Api
```

- **Api:** composition root, configuration, dependency injection, health checks, Problem Details exception middleware and development-only Swagger/OpenAPI.
- **Application:** reserved for actual use cases; references Domain but contains no invented contracts.
- **Domain:** independent and currently contains no product types.
- **Infrastructure:** EF Core 8.0.30, Npgsql EF provider 8.0.11 and an empty `KitchainDbContext`. It does not yet need an Application/Domain reference.
- **Tests:** xUnit with `WebApplicationFactory`, covering health behavior and the development-only OpenAPI boundary. No live database dependency.

`backend/global.json` selects SDK 8.0.424 with patch roll-forward. The API uses standard ASP.NET Core configuration and reads `ConnectionStrings:Kitchain` (environment key `ConnectionStrings__Kitchain`). No credentials are present in tracked application settings.

`GET /health` reports process liveness without probing the database. `GET /health/ready` checks PostgreSQL connectivity through the DbContext and propagates cancellation. Missing configuration or a failed connection produces an unhealthy result. Startup does not open a database connection, create tables, apply migrations or seed data.

`AddProblemDetails` and `UseExceptionHandler` establish standard exception responses. Health responses intentionally expose only the aggregate status, without connection details. Swagger middleware runs only in Development. Its business operation list is empty because no business routes exist; health-check middleware is verified directly via HTTP. The development profile binds to localhost:5080 over HTTP.

### PostgreSQL and Docker

Root Compose runs only `postgres:18`, a [supported major release](https://www.postgresql.org/support/versioning/). The tag accepts maintenance updates within major 18. The named volume mounts `/var/lib/postgresql`, matching the image's versioned data directory layout for PostgreSQL 18. The published port binds only to loopback. An internal `pg_isready` health check gates `compose up --wait`.

Development values come from ignored `.env`, copied from `.env.example`. The API connection string is set separately in its terminal; Compose environment loading does not configure a host-run .NET process. No schema or migration exists because there are no product entities. Angular and the API are not containerized.

## Planned, not implemented

Future modules will add actual domain types and use cases incrementally. Infrastructure may reference Application or Domain when a concrete contract requires it. The first persistence feature will introduce meaningful migrations and PostgreSQL integration tests.

Court-provider adapters, community moderation, SignalR sessions, queues, scoring, deterministic PaddleMatch, identity, background synchronization and analytics remain future work. There are no message brokers, caches, microservices, CQRS frameworks, deployment pipelines or production hosting configuration.
