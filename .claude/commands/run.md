# Run Project (API)

Start the API locally and confirm it's serving requests.

---

This repo is a single ASP.NET Core minimal API project (`CachingPoc.Api/`, .NET 10) — no frontend, no Docker/Compose/Kubernetes for the app itself. The only container involved in this POC is a standalone local Redis instance, introduced starting at Milestone 3 (see `docs/caching-poc-spec.md`).

## Run the API

```bash
dotnet run --project CachingPoc.Api
```

Port is whatever's shown in the console output (see `CachingPoc.Api/Properties/launchSettings.json` for the configured profiles).

## Run Redis (only needed from Milestone 3 onward)

```bash
docker run -p 6379:6379 redis
```

No compose file — a single container is enough for this POC.

## Verify it's up

```bash
curl -i http://localhost:<port>/products/1
```

Confirm the JSON response and note the `source` field (`"db"`, `"memory"`, or `"redis"` depending on which milestone is implemented) and the response latency, since proving cache behavior via timing is the point of this POC.

## Output

Report concisely:
- Port the API is listening on
- Result of hitting `/products/{id}` (status code, `source` field, rough latency)
- Whether Redis is running, if the milestone reached requires it
- Any errors (connection refused, missing migration/database, Redis connection failure)
