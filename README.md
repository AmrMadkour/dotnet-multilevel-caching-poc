# Multi-Level Caching POC (.NET)

Learning POC for multi-level caching in a real API: in-memory cache (L1) → Redis (L2) → SQLite database (source of truth), with visible proof at each layer that caching is actually working (via a `source` field and measured timing).

Full milestone plan: [`docs/caching-poc-spec.md`](docs/caching-poc-spec.md).

## Status

✅ **Milestone 1 complete** — baseline `GET /products/{id}` endpoint reading directly from SQLite via EF Core, no caching.
✅ **Milestone 2 complete** — `IMemoryCache` (L1): cache hits return `source: "memory"`, misses fall through to the DB and populate the cache.
✅ **Milestone 3 complete** — Redis (L2) via `StackExchange.Redis`: lookup order is memory → Redis → DB, and each miss populates the faster layer(s) above it.
✅ **Milestone 4 complete** — per-layer TTLs (memory 30s, Redis 2min — deliberately different, so L1 expiring before L2 is observable), `PUT /products/{id}` to update price + refresh both cache layers, `DELETE /cache/{id}` to manually clear a cached entry.

## Project structure

- `CachingPoc.Api/` — single ASP.NET Core minimal API project (.NET 10).
  - `Program.cs` — composition root: DI registration and middleware pipeline.
  - `Endpoints/` — route definitions, grouped as `IEndpointRouteBuilder` extension methods (e.g. `ProductEndpoints`).
  - `Models/` — entity classes (e.g. `Product`).
  - `Data/` — `AppDbContext` (EF Core).
  - `Migrations/` — EF Core migrations.
  - `CachingPoc.Api.http` — REST Client scratch file (VS/VS Code "Send Request") with sample `GET`/`PUT`/`DELETE` requests, for manual testing without Postman/`curl`.
- `docs/` — spec and design notes.

## Running locally

1. Start Redis in a local Docker container (required — the app connects to it on startup and will fail fast if it's unreachable):
   ```
   docker run -p 6379:6379 redis
   ```
2. Open `CachingPoc.Api.slnx` in Visual Studio 2022+.
3. Apply EF Core migrations to create the local SQLite database (Package Manager Console, with `CachingPoc.Api` set as the default project):
   ```
   Update-Database
   ```
   (Equivalently: `dotnet ef database update --project CachingPoc.Api`.)
4. Press **F5** (or `dotnet run --project CachingPoc.Api`).

The API listens on the port shown in the console / `CachingPoc.Api/Properties/launchSettings.json`. With OpenAPI enabled, you can browse `/openapi/v1.json` in development. Connection strings for both SQLite and Redis live in `CachingPoc.Api/appsettings.json` (`ConnectionStrings:Default` and `ConnectionStrings:Redis`).

## Endpoints

- `GET /products/{id}` — looks up a product through the cache chain: in-memory (L1, 30s TTL) → Redis (L2, 2min TTL) → SQLite (source of truth, artificial 1s delay to simulate an expensive query). Returns `source: "memory"`, `"redis"`, or `"db"` depending on which layer served the request, and populates any faster layer(s) that missed, with a fresh TTL.
- `PUT /products/{id}` — body `{ "price": 19.99 }`. Updates the price in SQLite (the source of truth for the write), then overwrites both cache layers with the fresh product and fresh TTLs, so a stale price is never served after an update.
- `DELETE /cache/{id}` — clears a product's entry from both cache layers, for demoing cache behavior on demand instead of waiting out a TTL. Returns `404` if the entry wasn't cached in either layer, `204` if it cleared something.

## Cache expiration (TTL)

Both cache layers use **absolute expiration** (`MemoryTtl` = 30s, `RedisTtl` = 2min) rather than sliding expiration:

- **Absolute expiration** — the entry expires at a fixed point in time from when it was set, regardless of how often it's read in between.
- **Sliding expiration** — the expiration clock resets on every read, so a frequently-accessed entry can live indefinitely.

Absolute was chosen deliberately: the goal is to *prove* L1 expires before L2 in a predictable window. Sliding expiration would reset on every test request, making that behavior non-deterministic and hard to demo.

## Testing cache behavior

To observe each layer distinctly, you'll want to clear one layer while leaving the other(s) intact:

- **Clear the in-memory (L1) cache**: stop and restart the API (`IMemoryCache` is in-process, so a restart wipes it). Redis stays populated, so the next request for a previously-cached id should return `source: "redis"` without touching the DB.
- **Clear Redis (L2)** — everything:
  ```
  docker exec -it <container_id_or_name> redis-cli FLUSHALL
  ```
- **Clear Redis (L2)** — a single product's key:
  ```
  docker exec -it <container_id_or_name> redis-cli DEL product:{id}
  ```
  Clearing Redis (with the in-memory cache still cold, e.g. right after a restart) forces the next request through to the DB, letting you confirm the miss path repopulates both cache layers correctly.
- **Observe TTL expiry**: call `GET /products/{id}` once (populates both layers), then wait 30+ seconds and call it again — expect `source: "redis"` (L1 expired, L2 still alive). Wait past 2 minutes total and call again — expect `source: "db"` (both expired).
- **Confirm writes invalidate stale data**: `GET` a product (caches it), `PUT` a new price, then `GET` again immediately — expect `source: "memory"` with the *new* price, proving the write refreshed the cache rather than leaving the old value behind.
- **Manually clear a cache entry**: `DELETE /cache/{id}` — first call returns `204` (something was cleared), a second immediate call returns `404` (nothing left to clear).

## Measured timings

Approximate, from a local run (loopback network, SQLite on local disk, Redis in a local Docker container):

| Source | Typical latency |
| --- | --- |
| `db` | ~1000ms (dominated by the artificial `Task.Delay(1000)`) |
| `redis` | low single-digit ms |
| `memory` | sub-millisecond |

*(Numbers will vary by machine — re-run the steps above and swap in your own measured values if you want exact figures for this repo.)*

## Going to production

This POC intentionally skips several concerns that a real deployment would need. None of the following is implemented here — it's guidance for what you'd configure if this pattern went to production:

- **Clustering**: a single Redis instance is a single point of failure. In production, either enable [Redis Cluster mode](https://redis.io/docs/management/scaling/) (sharding across multiple nodes, each with replicas) or use a managed clustered offering (Azure Cache for Redis Premium/Enterprise tier, AWS ElastiCache with cluster mode enabled). `StackExchange.Redis`'s `ConnectionMultiplexer` already understands cluster topologies — the main change is the connection string pointing at cluster endpoints instead of a single host.
- **Persistence**: by default Redis is purely in-memory — a restart loses everything, forcing every key back through the DB at once (a "thundering herd"). In `redis.conf`, enable either:
  - **RDB snapshotting** (`save <seconds> <changes>`) — periodic point-in-time snapshots to disk. Cheap, fast to restart from, but can lose the last few minutes of writes on a crash.
  - **AOF (Append Only File)** (`appendonly yes`) — logs every write operation; replay on restart. Much lower data-loss window, but larger files and slower restarts than RDB. Many production setups enable both (RDB for fast full restores, AOF for durability), and let Redis reconcile them on startup.
- **Auth**: this POC's Redis instance accepts unauthenticated connections from anything that can reach port 6379. In production, set `requirepass <strong-password>` in `redis.conf` (or use [ACLs](https://redis.io/docs/management/security/acl/) for per-user/per-command permissions instead of one shared password), and — just as importantly — network-isolate Redis so it's only reachable from the app's own subnet/VPC (security group rules, private networking), never exposed to the public internet.
