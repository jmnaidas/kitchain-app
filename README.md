# Kitchain

**Your pickleball companion.**

Kitchain is a full-stack pickleball companion taking shape in Metro Manila, Philippines. Its three product pillars are **Courts** (where to play), **Play** (getting together and managing the game), and **Gear** (what to play with). PaddleMatch will belong inside Gear.

## Status: foundation phase

The repository currently contains a responsive Angular shell with honest introduction pages, an ASP.NET Core API with health checks, and local PostgreSQL configuration. There are **no product endpoints, domain entities, database tables, accounts, or live court/paddle/session data**.

## Stack and architecture

- Angular 22.0.8 / CLI 22.0.9, TypeScript 6, standalone components, Router and SCSS
- Lucide (`@lucide/angular`), Motion (`motion/mini`); Three.js installed for later use
- ASP.NET Core 8, EF Core 8 and Npgsql
- PostgreSQL 18 through Docker Compose
- Angular ESLint, Prettier, Vitest and xUnit integration tests

A modular monolith foundation: one frontend, one API process, one database. The API composes Application and Infrastructure; Application references Domain. Domain is independent. Application and Domain have no invented types. Infrastructure currently only contains the DbContext. See [architecture](docs/architecture.md).

```text
frontend/             Angular application, tests and tooling
backend/
  Kitchain.Api/       HTTP composition, health checks, development Swagger
  Kitchain.Application/
  Kitchain.Domain/
  Kitchain.Infrastructure/  EF Core DbContext
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
dotnet run --project Kitchain.Api --launch-profile http
```

ASP.NET Core does **not** read the root Compose `.env` automatically. The API reads standard .NET configuration, including `ConnectionStrings__Kitchain` from the environment. Adjust the port and credentials above if you changed `.env`. No connection string or production secret is stored in tracked API settings.

- [Liveness](http://localhost:5080/health): `200 Healthy`, independently of PostgreSQL
- [Readiness](http://localhost:5080/health/ready): `200 Healthy` when PostgreSQL connects; `503 Unhealthy` otherwise
- [Swagger UI](http://localhost:5080/swagger) and [OpenAPI JSON](http://localhost:5080/swagger/v1/swagger.json): development only

The API starts without database configuration; readiness then reports unhealthy. It does not create a schema or run migrations. Swagger has no business operations yet; health middleware routes are checked using the URLs above or `Kitchain.Api.http`. Local HTTP is intentional for this foundation; production hosting is out of scope.

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

Frontend tests verify navigation, all four routes, active state and honest scope messaging. Backend tests exercise liveness, missing-database readiness, and the development-only OpenAPI boundary. Tests do not require Docker. Live PostgreSQL connectivity is verified through `/health/ready`; no Testcontainers, Playwright or Storybook is installed.

## Scope and next direction

The shell establishes responsive navigation, a small provisional token layer, visible keyboard focus, reduced-motion support and restrained editorial typography. Three.js is installed but not imported or bundled into the application. The wordmark is plain text, not a final logo.

Future delivery will incrementally support **Discover → Book → Queue → Play → Score → Track**, alongside Gear discovery and explainable PaddleMatch. Court provider integrations, community contributions, fair queues, scoring, accounts and analytics remain planned. See [product overview](docs/product-overview.md). The next step is to define the first narrow Courts discovery slice and its data provenance before adding persistence and real UI.
