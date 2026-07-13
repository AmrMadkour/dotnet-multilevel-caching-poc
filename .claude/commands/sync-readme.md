# Update README.md

Read `README.md`, then check: git diff, new/modified files, architecture/dependency/command changes.

Update **only** where something meaningfully changed. Keep it concise, professional, and readable by both developers and recruiters.

## Quality check
Verify a new developer can:
1. Clone → run via `docker compose up` (API + Postgres) → run via k8s manifests on a local cluster → hit `/health` and `/notes`

If any setup/run commands are missing, outdated, or wrong — fix them using actual project paths and scripts.

## Rules
- No implementation detail, incomplete features, or speculative architecture
- No duplicate content
- Preserve existing formatting where possible

## After updating, report
1. Sections changed and why
2. Any setup/run instructions fixed
