# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A learning POC for multi-level caching in .NET: in-memory cache (L1) → Redis (L2) → SQLite database (source of truth). The full milestone-by-milestone plan lives in `docs/caching-poc-spec.md` — read it before making changes, since it defines exactly what each stage of the app should do and in what order.

The user is building this hands-on in Visual Studio to learn the concepts, pasting/reviewing code chunk by chunk rather than having it generated wholesale. When assisting in this repo, bias toward explaining *why* a piece of code is needed, not just producing it.

## Commands

- Build: `dotnet build CachingPoc.Api`
- Run: `dotnet run --project CachingPoc.Api` (or F5 in Visual Studio)
- EF Core migrations (from repo root, or add `--project CachingPoc.Api` if run outside it):
  - `dotnet ef migrations add <Name> --project CachingPoc.Api`
  - `dotnet ef database update --project CachingPoc.Api`
  - (Equivalently, in Visual Studio's Package Manager Console: `Add-Migration <Name>`, `Update-Database`.)

No test project exists yet.

## Architecture

- Single project: `CachingPoc.Api/` — an ASP.NET Core **minimal API** (no controllers) targeting **.NET 10**.
- `Program.cs` is the composition root: service registration (DI) and middleware pipeline live here — no controller classes. Route definitions are grouped into extension methods under `CachingPoc.Api/Endpoints/` (namespace `CachingPoc.Api.Endpoints`), e.g. `ProductEndpoints.MapProductEndpoints(this IEndpointRouteBuilder app)`, called once from `Program.cs` as `app.MapProductEndpoints()`.
- Entity classes live under `CachingPoc.Api/Models/` (namespace `CachingPoc.Api.Models`) — e.g. `Product` (`Id`, `Name`, `Price`). No Data Annotations are used; EF Core conventions (Id → key, non-nullable reference types → `NOT NULL`) cover what's needed for this POC.
- `AppDbContext` lives under `CachingPoc.Api/Data/` (namespace `CachingPoc.Api.Data`), registered via `AddDbContext` in `Program.cs` with its connection string read from `ConnectionStrings:Default` in `appsettings.json`.
- Data access is EF Core over SQLite (`Microsoft.EntityFrameworkCore.Sqlite`), with `Microsoft.EntityFrameworkCore.Design`/`.Tools` providing migration tooling. The SQLite file is a local artifact, not committed.
- `SQLitePCLRaw.lib.e_sqlite3`'s known advisory (GHSA-2m69-gcr7-jv3q) is suppressed via `NuGetAuditSuppress` in the `.csproj` — no fixed upstream package exists yet as of this POC; revisit when one ships.
- The API is being built up in the milestone order defined in `docs/caching-poc-spec.md`:
  1. DB-only baseline (`source: "db"`, artificial delay to simulate a slow query)
  2. Add `IMemoryCache` (L1) — cache hits report `source: "memory"`
  3. Add Redis (L2, via `StackExchange.Redis`) — cache hits report `source: "redis"`, and populate faster layers on a miss
  4. TTLs (different per layer, deliberately) + write-path invalidation
  5. README + a documented (not implemented) "going to production" section for clustering/persistence/auth

  Every response is expected to report which layer served it (`source` field), so caching behavior is provable via response + timing, not assumed.
- Redis itself runs as a single local Docker container (`docker run -p 6379:6379 redis`) starting at Milestone 3 — the .NET app itself is not containerized in this POC.

## Current progress

**Milestone 1 (baseline DB-only endpoint) — complete:**

- [x] Project created: `CachingPoc.Api` (.NET 10 minimal API, OpenAPI enabled)
- [x] NuGet packages added: `Microsoft.EntityFrameworkCore.Sqlite`, `.Design`, `.Tools`
- [x] `Product` model created (`CachingPoc.Api/Models/Product.cs`)
- [x] `AppDbContext` (DbSet + `OnModelCreating` seed data via `HasData`) — `CachingPoc.Api/Data/AppDbContext.cs`
- [x] SQLite connection string in `appsettings.json` (`ConnectionStrings:Default`)
- [x] Registered `AppDbContext` in `Program.cs` (`AddDbContext`)
- [x] Ran `Add-Migration InitialCreate` / `Update-Database` — `.db` file + seed rows created
- [x] `GET /products/{id}` endpoint (`Task.Delay(500)` + `source: "db"`) — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`, replacing the template's `/weatherforecast`
- [x] Verified baseline: ~500ms per call, every time, via `CachingPoc.Api.http`

**Milestone 2 (add `IMemoryCache` L1) — complete:**

- [x] `builder.Services.AddMemoryCache()` registered in `Program.cs`
- [x] `IMemoryCache cache` parameter added to the `GetProduct` handler signature in `ProductEndpoints.cs`
- [x] Cache-check/populate logic (`TryGetValue` → `source: "memory"` on hit; `Set` after DB fetch on miss) — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] Verified: first call ~500ms+ `source: "db"`, second call near-instant `source: "memory"`

**Milestone 3 (add Redis L2) — complete:**

- [x] Redis running locally via `docker run -p 6379:6379 redis`
- [x] `StackExchange.Redis` NuGet package added
- [x] Redis connection string in `appsettings.json` (`ConnectionStrings:Redis`)
- [x] `IConnectionMultiplexer` registered as a singleton in `Program.cs`
- [x] `GetProduct` extended to 3-tier lookup (memory → Redis → DB), each miss populating the faster layer(s) — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] Verified: memory hit → `source: "memory"`; app restart + Redis still populated → `source: "redis"`; cold miss on both → `source: "db"`, repopulates both layers

**Milestone 4 (TTLs + write-path invalidation) — complete:**

- [x] Endpoint handlers split into named static methods (`GetProduct`, `UpdateProductPrice`, `ClearProductCache`) instead of inline lambdas in `MapProductEndpoints` — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] Absolute expiration added to both cache layers, deliberately different (`MemoryTtl` = 30s, `RedisTtl` = 2min) — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] `PUT /products/{id}` (`UpdateProductPriceRequest` body) updates the DB row, then overwrites both cache layers with the fresh product + fresh TTLs — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] `DELETE /cache/{id}` checks memory + Redis for an existing entry first (404 if neither has it), otherwise clears both layers — `CachingPoc.Api/Endpoints/ProductEndpoints.cs`
- [x] `CachingPoc.Api.http` updated with `GET`/`PUT`/`DELETE` sample requests (replacing stale `/weatherforecast` sample)
- [x] User confirmed working end-to-end (memory/Redis/DB fallthrough, TTL expiry, write invalidation, cache-clear 204/404)

**Milestone 5 (README + GitHub) — complete:**

- [x] `README.md` updated: Milestone 4 status, `PUT`/`DELETE` endpoint docs, TTL/invalidation testing steps, approximate measured timings table, "Going to production" section (clustering, persistence, auth — documented only, not implemented)
- [x] Pushed to GitHub (`origin/main` @ `db02791`)

**All 5 milestones complete — POC finished per `docs/caching-poc-spec.md`.**

## How we work in this repo

- The user builds this hands-on in Visual Studio (not VS Code), to learn the concepts — not delegating code generation wholesale.
- Code is delivered as **ready-to-paste chunks** (one file/concept at a time), each with a line-by-line explanation of *why*, not just *what*. The user pastes into VS, confirms it builds/runs, then we move to the next chunk — don't dump multiple files at once.
- The assistant does not edit application source files directly — only the user pastes code into Visual Studio. (Non-code repo files — docs, `CLAUDE.md`, `README.md`, `.gitignore`, `.claude/commands/*` — are fine for the assistant to edit directly.)
- The full plan for this milestone is tracked via the task list (`TaskList`/`TaskUpdate`) — check it at the start of a new session to see what's done vs. pending before assuming where things left off.
- Full milestone plan and goals: `docs/caching-poc-spec.md`.

## Non-goals (per spec)

- No Docker/Kubernetes deployment of the .NET app itself.
- No distributed cache invalidation across multiple app instances — single app instance only.
- No production-grade Redis configuration (clustering, persistence, auth) — that's documented as guidance in the README, not implemented.
