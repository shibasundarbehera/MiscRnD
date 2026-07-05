## Bulk Ingestion API (.NET 8)

High-throughput bulk data pipeline: a lightweight metadata hub receives S3/MinIO file notifications, queues tasks in MongoDB, and stateless workers process Parquet/JSON files into application collections via unordered bulk writes.

```
PySpark Stream → S3/MinIO (Parquet blocks)
       ↓ ObjectCreated event
BulkApi.Api (202 Accepted, <50ms)
       ↓ PENDING task in MongoDB
BulkApi.Worker (poll + atomic lease)
       ↓ Parquet/JSON read → massage → bulkWrite(5000)
MongoDB records collection
```

### Projects

| Project | Role |
|---------|------|
| `BulkApi.Api` | Ultra-lightweight ingestion hub — indexes file paths, returns 202 immediately |
| `BulkApi.Worker` | Stateless background processor — leases tasks, reads files, bulk writes |
| `BulkApi.Core` | Shared models, MongoDB repo, S3 client, file readers, data massage |

### Quick start

```bash
# 1. Start dependencies
docker compose up -d

# 2. Run the ingestion API (terminal 1)
dotnet run --project src/BulkApi.Api

# 3. Run workers — scale horizontally (terminal 2+)
dotnet run --project src/BulkApi.Worker
```

API: http://localhost:5000/swagger  
MinIO console: http://localhost:9001 (minioadmin / minioadmin)

### Enqueue a file manually

```bash
curl -X POST http://localhost:5000/api/v1/ingest/file \
  -H "Content-Type: application/json" \
  -d '{"bucket":"bulk-ingest","objectKey":"batch/2026/07/05/data.parquet"}'
```

### S3 / MinIO webhook (ObjectCreated)

```bash
curl -X POST http://localhost:5000/api/v1/ingest/s3-event \
  -H "Content-Type: application/json" \
  -d '{
    "Records": [{
      "eventName": "ObjectCreated:Put",
      "s3": {
        "bucket": {"name": "bulk-ingest"},
        "object": {"key": "batch/2026/07/05/part-00001.parquet"}
      }
    }]
  }'
```

### Configuration

Both `BulkApi.Api` and `BulkApi.Worker` share the same `appsettings.json` sections:

| Section | Purpose |
|---------|---------|
| `MongoDB` | Connection string, task + data collection names |
| `S3` | MinIO/AWS endpoint, credentials, path-style access |
| `Worker` | Poll interval, lease TTL, batch size (5000), concurrency, idempotency key fields |

### Processing flow

1. **Ingest** — API inserts `{ bucket, objectKey, status: PENDING }` with dedupe index on `(bucket, objectKey)`.
2. **Lease** — Worker atomically `findOneAndUpdate`s the oldest PENDING task to PROCESSING (reclaims stale leases).
3. **Read** — Worker streams file from S3; Parquet via columnar Parquet.Net 6, JSON/JSONL/NDJSON via `System.Text.Json`.
4. **Massage** — Cleans strings, coerces types, adds `_ingested_at`, `_source_file`, deterministic `_id` (SHA-256 of idempotency keys).
5. **Write** — Unordered `bulkWrite` upserts in batches of 5,000 documents.
6. **Complete** — Task marked COMPLETED with row count.

### Supported file formats

- `.parquet` — columnar read via Parquet.Net (vectorized row-group processing)
- `.json` — JSON array or single object
- `.jsonl` / `.ndjson` — newline-delimited JSON

### Horizontal scaling

Run multiple worker instances. Each uses atomic MongoDB leasing — no coordination required. Tune `Worker:MaxConcurrentTasks` per instance.

### Docker (production)

```bash
docker build -f Dockerfile.api -t bulk-api .
docker build -f Dockerfile.worker -t bulk-api-worker .
```
