# Multi-Level Caching POC (.NET)

Learning POC for multi-level caching in a real API: in-memory cache (L1) → Redis (L2) → SQLite database (source of truth), with visible proof at each layer that caching is actually working (via a `source` field and measured timing).

Full milestone plan: [`docs/caching-poc-spec.md`](docs/caching-poc-spec.md).

## Status

🚧 **Milestone 1 in progress** — baseline `GET /products/{id}` endpoint reading directly from SQLite via EF Core, no caching yet.

## Project structure

- `CachingPoc.Api/` — single ASP.NET Core minimal API project (.NET 10). Holds `Program.cs`, the `Product` model, `AppDbContext`, and EF Core migrations.
- `docs/` — spec and design notes.

## Running locally

Open `CachingPoc.Api.slnx` in Visual Studio 2022+ and press **F5** (or `dotnet run --project CachingPoc.Api`).

The API listens on the port shown in the console / `CachingPoc.Api/Properties/launchSettings.json`. With OpenAPI enabled, you can browse `/openapi/v1.json` in development.

## Endpoints (so far)

- `GET /products/{id}` — fetches a product from SQLite (artificial 500ms delay to simulate an expensive query). Returns `source: "db"`.

More endpoints and caching layers land in later milestones (see the spec doc).
