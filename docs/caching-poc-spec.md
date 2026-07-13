# POC Spec: Multi-Level Caching (In-Memory + Redis) — .NET

## Purpose
Learning POC to understand multi-level caching in a real API: in-memory cache (L1) → Redis (L2) → database (source of truth), with visible proof at each layer that caching is actually working (via response source + measured timing), not just trusted blindly. This is a standalone POC, unrelated to any prior Docker/Kubernetes project.

---

## Milestone 1 — Baseline: DB-only endpoint, no caching

**What to build:**
- .NET minimal API.
- **SQLite** database (file-based, zero external setup) with a `Products` table: `Id`, `Name`, `Price`. Seed ~5-10 rows.
- `GET /products/{id}` — fetches from SQLite via EF Core.
- Add an artificial delay (e.g. `await Task.Delay(500)`) inside the DB fetch, to simulate an "expensive" query so later cache improvements are clearly visible against a tiny dataset.
- Response includes a `source: "db"` field (or response header) — this field will later report `"memory"` or `"redis"` too, so cache hits are provable, not assumed.

**Goal:** confirm baseline behavior — every call takes ~500ms, always hits the DB.

---

## Milestone 2 — Add in-memory cache (L1)

**What to build:**
- Wire in .NET's built-in `IMemoryCache`.
- `GET /products/{id}` logic becomes:
  1. Check in-memory cache for this id.
  2. If found → return immediately, `source: "memory"`.
  3. If not found → fetch from DB (slow path from Milestone 1), store result in memory cache, return with `source: "db"`.

**Goal:** prove first call is slow (~500ms, `source: db`), second call for the same id is near-instant (`source: memory`).

---

## Milestone 3 — Add Redis (L2)

**What to build:**
- Run Redis locally via a single Docker container (`docker run -p 6379:6379 redis`) — simplest possible setup, no compose needed for this POC.
- Add `StackExchange.Redis` (or `Microsoft.Extensions.Caching.StackExchangeRedis`) client.
- Updated lookup order:
  1. Check in-memory cache → hit → `source: "memory"`.
  2. Miss → check Redis → hit → `source: "redis"`, **also populate in-memory cache** so the next call is served from memory.
  3. Miss on both → hit DB (slow) → `source: "db"`, populate **both** Redis and in-memory cache.

**Goal:** prove the 3-tier behavior distinctly — clear only the in-memory cache (e.g. via an admin/test endpoint or app restart) and confirm the next call is served from Redis (fast, but not as fast as memory) without touching the DB at all.

---

## Milestone 4 — TTLs and invalidation

**What to build:**
- Add expiration to both cache layers (e.g. in-memory: 30s TTL, Redis: 2min TTL — deliberately different, to make the "L1 expires before L2" behavior observable).
- Add a way to update a product's price (`PUT /products/{id}`) that also invalidates/updates both cache layers, so stale data isn't served after a write.
- Optional: a `DELETE /cache/{id}` test/admin endpoint to manually clear both layers on demand, useful for demoing behavior deliberately rather than waiting out TTLs.

**Goal:** prove cache correctly expires and refreshes, and that writes don't leave stale cached data behind.

---

## Milestone 5 — GitHub + README

- Push the project.
- README documents: what each milestone proves, and real captured timing numbers (e.g. "DB hit: ~500ms, Redis hit: ~5ms, memory hit: <1ms") as evidence, not just claims.
- README includes a **"Going to production" section**, documenting — as steps/guidance, not implemented — how each skipped concern would actually be configured if this were a real production system:
  - **Clustering**: brief steps on enabling Redis Cluster mode (or using a managed clustered offering like Azure Cache for Redis/AWS ElastiCache) instead of a single instance.
  - **Persistence**: how to enable RDB snapshotting and/or AOF logging in `redis.conf`, and the trade-offs between them.
  - **Auth**: how to set `requirepass` (or configure ACLs) and network-isolate the Redis instance so it isn't reachable from outside trusted infrastructure.
- This section is documentation only — the POC itself stays simple and unconfigured; the README just captures the "what you'd do next" knowledge so it isn't lost.

---

## Explicit non-goals
- No Docker/Kubernetes deployment of this POC — runs locally only (except Redis itself, which runs in one simple container).
- No distributed cache invalidation across multiple app instances (that's a real multi-level-cache complexity, but out of scope — this POC runs as a single app instance).
- No production-grade Redis config (clustering, persistence tuning, auth) — single local instance is enough to learn the pattern.
