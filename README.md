# Kitchain

**Your pickleball companion.**

Kitchain is a full-stack pickleball companion taking shape in Metro Manila, Philippines. Its three product pillars are **Courts** (where to play), **Play** (getting together and managing the game), and **Gear** (what to play with). PaddleMatch will belong inside Gear.

## Status: Phase 2A — Courts discovery backend

The repository contains a responsive Angular shell, an ASP.NET Core API with health checks and read-only Courts discovery, and PostgreSQL persistence. A Court represents a venue containing physical courts. The Angular pages remain placeholders; availability, real booking integrations, accounts, Play and Gear functionality are not implemented. All development venue records are explicitly fictional.

## Stack and architecture

- Angular 22.0.8 / CLI 22.0.9, TypeScript 6, standalone components, Router and SCSS
- Lucide (`@lucide/angular`), Motion (`motion/mini`); Three.js installed for later use
- ASP.NET Core 8, EF Core 8 and Npgsql
- PostgreSQL 18 through Docker Compose
- Angular ESLint, Prettier, Vitest and xUnit integration tests

A modular monolith: one frontend, one API process, one database. The API composes Application and Infrastructure; Application references Domain. Domain is independent. Courts queries flow through an application service and a focused read interface implemented with EF Core in Infrastructure. See [architecture](docs/architecture.md).

```text
frontend/             Angular application, tests and tooling
backend/
  Kitchain.Api/       HTTP composition, health checks, development Swagger
  Kitchain.Application/  Courts discovery use case and contracts
  Kitchain.Domain/       Venue aggregate and amenity vocabulary
  Kitchain.Infrastructure/  EF queries, configuration, migration, sample seeder
  Kitchain.Tests/     API integration tests
  Kitchain.sln
docs/                 Product direction and implemented architecture
docker-compose.yml    Local PostgreSQL only
.env.example          Disposable local database credentials
```

## Prerequisites

- Node.js 24.19.0 (the verified local version) and npm 11.17.0
- .NET SDK 8.0.424 or a later patch in the 8.0.4xx feature band (`backend/global.json`)
- Docker Desktop with Linux containers and Docker Compose
- Git

Angular 22.0 officially supports Node `^22.22.3 || ^24.15.0 || ^26.0.0`; Node 24.19.0 satisfies that range. See the [official compatibility table](https://angular.dev/reference/versions). No machine-wide tooling changes are required.

## Local development

Run these PowerShell commands from the repository root unless stated otherwise.

### 1. Start PostgreSQL

```powershell
Copy-Item .env.example .env
docker compose config --quiet
docker compose up -d --wait
docker compose ps
```

Only copy the example on first setup; preserve your existing `.env` thereafter. The database is bound to `127.0.0.1:5432`. `.env` is ignored, and its example values are strictly for local development. PostgreSQL persists in the `kitchain_postgres_data` named volume. `docker compose stop` stops the service without deleting data. Do not run `down -v` unless you intend to erase the local database.

Changing the username/password in `.env` does not change credentials in an already initialized PostgreSQL volume. Update database credentials explicitly if needed.

### 2. Run the API

In the backend terminal, set a connection string matching your `.env`:

```powershell
$env:ConnectionStrings__Kitchain = "Host=localhost;Port=5432;Database=kitchain;Username=kitchain_dev;Password=kitchain_local_only"
cd backend
dotnet restore Kitchain.sln
dotnet tool restore
dotnet ef database update --project Kitchain.Infrastructure --startup-project Kitchain.Api
# Optional, Development only: add seven clearly fictional sample venues.
dotnet run --project Kitchain.Api --launch-profile http -- --seed-courts
dotnet run --project Kitchain.Api --launch-profile http
```

ASP.NET Core does **not** read the root Compose `.env` automatically. The API reads standard .NET configuration, including `ConnectionStrings__Kitchain` from the environment. Adjust the port and credentials above if you changed `.env`. No connection string or production secret is stored in tracked API settings.

- [Liveness](http://localhost:5080/health): `200 Healthy`, independently of PostgreSQL
- [Readiness](http://localhost:5080/health/ready): `200 Healthy` when PostgreSQL connects; `503 Unhealthy` otherwise
- [Swagger UI](http://localhost:5080/swagger) and [OpenAPI JSON](http://localhost:5080/swagger/v1/swagger.json): development only

The API starts without database configuration; readiness then reports unhealthy. Normal startup does not migrate or seed. Apply `AddCourtsDiscovery` explicitly with the local EF tool. The migration creates `Courts`, `Amenities`, and `CourtAmenities` and installs the 13 amenity lookup codes. The separate seed command adds missing sample IDs only, preserving existing records; it exits without starting the server and refuses to run outside Development. Five samples are Published, one Draft, and one Inactive. Names include `Demo` and `(fictional)`, addresses are fictional, and URLs use `example.com`.

### Courts read API

- `GET /api/courts` — published venues with pagination
- `GET /api/courts/{id}` — richer published-venue details; missing, Draft and Inactive IDs return 404

Example: [filtered discovery](http://localhost:5080/api/courts?city=Makati&indoorOutdoor=Indoor&amenity=Parking&page=1&pageSize=20).

Optional filters: `city` (trimmed, case-insensitive exact match), `indoorOutdoor` (`Indoor`, `Outdoor`, `Mixed`), `minCourts`, `maxStartingPrice`, `currencyCode`, and one `amenity` code. Filters combine with AND. `Mixed` is a separate classification, not implicitly included in Indoor or Outdoor. A price cap excludes unknown prices and defaults to PHP unless `currencyCode` is supplied; prices are never converted or compared across currencies. Prices are approximate starting amounts, not booking quotes; `priceUnit` describes the basis.

Pagination defaults to `page=1&pageSize=20`, allows page sizes 1–100 and pages 1–1,000,000, and orders by Name then Id. The response has `items`, `page`, `pageSize`, `totalCount`, and `totalPages`. A page beyond the end returns an empty item list with the filtered total. Invalid query values return 400 Problem Details.

Responses use string enums and explicit DTOs. `availability.status` is always `NotIntegrated`, which does **not** mean the venue is unavailable. Contact/booking fields point outward; Kitchain does not process bookings. Swagger documents the new routes; `Kitchain.Api.http` contains runnable examples. Local HTTP remains intentional; production hosting is out of scope.

### 3. Run the frontend

In another terminal:

```powershell
cd frontend
npm ci
npm start
```

Open [localhost:4200](http://localhost:4200). Routes: `/`, `/courts`, `/play`, `/gear`. The frontend does not make API requests yet, so no proxy or CORS policy is needed. Use Angular HttpClient when real HTTP behavior begins.

## Tests and quality checks

```powershell
cd frontend
npm run lint
npm test -- --watch=false
npm run build
npm run format:check
```

```powershell
cd backend
dotnet restore Kitchain.sln
dotnet build Kitchain.sln --no-restore
dotnet test Kitchain.sln --no-build --no-restore
```

Frontend tests verify navigation, all four routes, active state and honest scope messaging. Backend unit tests cover domain invariants and query normalization; the existing health/OpenAPI tests remain. PostgreSQL integration tests exercise real migrations, seeding, filters, pagination, publication boundaries, contracts and validation through the API.

To include PostgreSQL integration tests, start Compose and set this in the backend terminal before `dotnet test`:

```powershell
$env:KITCHAIN_TEST_CONNECTION_STRING = $env:ConnectionStrings__Kitchain
dotnet test Kitchain.sln
```

Use a local development database account with schema-creation permission. Each integration run creates a unique `kitchain_test_<guid>` schema, uses it for all test data and migration history, then drops only that test schema. Existing development tables are not modified. Without the variable, these integration tests are explicitly reported as skipped; they do not silently substitute an in-memory provider. No Testcontainers, Playwright or Storybook is installed.

## Scope and next direction

The shell establishes responsive navigation, a small provisional token layer, visible keyboard focus, reduced-motion support and restrained editorial typography. Three.js is installed but not imported or bundled into the application. The wordmark is plain text, not a final logo.

Future delivery will incrementally support **Discover → Book → Queue → Play → Score → Track**, alongside Gear discovery and explainable PaddleMatch. Court provider integrations, availability, community contributions, fair queues, scoring, accounts and analytics remain planned. See [product overview](docs/product-overview.md). The next step is Phase 2B: a deliberate Courts Explore frontend using the read API.
