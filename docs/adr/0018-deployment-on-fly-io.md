# Deploy one container to Fly.io, with daily off-host backups and a local deploy script

The co-op runs as a single Docker image on one always-on Fly.io Machine. Cost is the constraint: about $2.19 a month for a shared-cpu 256MB Machine plus $0.15 per GB-month for the volume (Fly's pricing page, 2026-10-27).

**Host.** Fly.io, region `iad` (Europe costs about 15% more, so members' data is not held in the EU), one shared-cpu 256MB Machine with a 1GB volume holding the database. The Machine never stops (`auto_stop_machines = false`, `min_machines_running = 1`) because Settle runs in a hosted service (ADR 0007), and there is never a second instance: SQLite and the rate-limit file (ADR 0004, ADR 0014) are single-instance. Hetzner (about 5 euro a month, but we would run Docker, TLS and patching) and Railway or Render (more expensive for the same job) were rejected. Oracle's free tier was rejected as unreliable.

**Image and URL.** One multi-stage image: the frontend is built and copied into the API's `wwwroot`, and the API serves it with a fallback to `index.html` for client routes, excluding `/api` and `/health`. This keeps one origin (ADR 0006), so no CORS. The app launches on the free `<app>.fly.dev` URL with Fly's TLS; a custom domain later is configuration (the magic-link base URL) and a redeploy.

**Email.** Production uses the existing `SmtpEmailSender` (ADR 0012) over Gmail SMTP with an app password, set as the Fly secrets `Email__Smtp__Address` and `Email__Smtp__Password`. No code change. Gmail will make mail more likely to land in spam, and the app password belongs to a personal account; moving to a transactional provider later is configuration only.

**Client IP.** Behind Fly's proxy the app trusts Fly's proxy network for forwarded headers rather than one `KnownProxies` address (ADR 0014 amendment). The exact addresses and header are verified in the runbook ticket.

**Backups.** A daily job in the container takes a consistent snapshot of the main database only (the rate-limit file needs none) and uploads it with `rclone` to a free-tier object store (Cloudflare R2 or Backblaze B2), keeping 30 daily snapshots. `scripts/restore-drill.sh` restores the latest snapshot into a scratch database and checks that it opens and the Ledger triggers exist; it runs once before launch and then quarterly, by calendar reminder. Litestream was rejected as a long-running sidecar the co-op does not need.

**Continuous delivery.** None on GitHub (ADR 0015). `scripts/deploy.sh` refuses to run unless the full-gate stamp matches the current tree, then runs `fly deploy`. Deploys are deliberate and local; migrations apply at startup (ADR 0004).

**Follow-up tickets.** #146 Dockerfile and SPA serving; #147 `fly.toml` and the deploy script; #148 backup job and restore drill; #149 production runbook and Gmail configuration.
