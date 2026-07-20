# Multi-Level Caching POC (.NET)

Learning POC for multi-level caching in a real API: in-memory cache (L1) → Redis (L2) → SQLite database (source of truth), with visible proof at each layer that caching is actually working (via a `source` field and measured timing).

Full milestone plan: [`docs/caching-poc-spec.md`](docs/caching-poc-spec.md).

## Status

✅ **Milestone 1 complete** — baseline `GET /products/{id}` endpoint reading directly from SQLite via EF Core, no caching.
🚧 **Milestone 2 in progress** — wiring in `IMemoryCache` (L1).

## Project structure

- `CachingPoc.Api/` — single ASP.NET Core minimal API project (.NET 10).
  - `Program.cs` — composition root: DI registration and middleware pipeline.
  - `Endpoints/` — route definitions, grouped as `IEndpointRouteBuilder` extension methods (e.g. `ProductEndpoints`).
  - `Models/` — entity classes (e.g. `Product`).
  - `Data/` — `AppDbContext` (EF Core).
  - `Migrations/` — EF Core migrations.
- `docs/` — spec and design notes.

## Running locally

1. Open `CachingPoc.Api.slnx` in Visual Studio 2022+.
2. Apply EF Core migrations to create the local SQLite database (Package Manager Console, with `CachingPoc.Api` set as the default project):
   ```
   Update-Database
   ```
   (Equivalently: `dotnet ef database update --project CachingPoc.Api`.)
3. Press **F5** (or `dotnet run --project CachingPoc.Api`).

The API listens on the port shown in the console / `CachingPoc.Api/Properties/launchSettings.json`. With OpenAPI enabled, you can browse `/openapi/v1.json` in development.

## Endpoints (so far)

- `GET /products/{id}` — fetches a product from SQLite (artificial 500ms delay to simulate an expensive query). Returns `source: "db"`.

More endpoints and caching layers land in later milestones (see the spec doc).
