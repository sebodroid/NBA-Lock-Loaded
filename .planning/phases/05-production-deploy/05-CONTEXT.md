# Phase 5: Production Deploy - Context

**Gathered:** 2026-03-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Get the working local application running reliably at a public URL. Scope is:
- Production Docker Compose (API + Worker + Nginx containers, no local PostgreSQL)
- Nginx container as the reverse proxy gateway for the DigitalOcean Droplet
- Secrets management via .env file on the server (never committed)
- Automated CI/CD pipeline (GitHub Actions → GHCR → Droplet + Cloudflare Pages)
- Cloudflare Access gate restricting who can reach the app

Database (Aiven PostgreSQL) and React SPA hosting (Cloudflare Pages) are external services — this phase wires them into the production runtime, not builds them.

</domain>

<decisions>
## Implementation Decisions

### Nginx placement & SSL
- Nginx runs as a **container inside docker-compose.prod.yml** alongside API and Worker
- **Cloudflare proxy handles SSL** (Flexible/Full) — Nginx only sees HTTP internally, no cert management needed
- Nginx scope: **API gateway only** — proxies `/api/*` to the API container; React SPA is served by Cloudflare Pages, not Nginx
- Nginx config delivered via **volume mount from the Droplet** (nginx.conf lives on server, easy to update without rebuilding)

### Secrets injection
- Secrets reach containers via a **.env file on the DigitalOcean Droplet**, referenced by docker-compose.prod.yml `env_file` directive
- **.env.example committed to repo** with placeholder values; developer fills in real values on the server manually (SSH in once to create)
- **Same variable names** as dev .env — no prod-specific naming, docker-compose.prod.yml uses identical env var keys
- **No postgres service in docker-compose.prod.yml** — Aiven connection string provided via .env; include a comment: `# PostgreSQL: Aiven cloud DB — see .env for connection string`

### Deployment workflow (DigitalOcean Droplet)
- **Automated GitHub Actions** on push to main
- Workflow: build Docker images in CI → push to **GitHub Container Registry (GHCR)** → SSH to Droplet → pull new images → restart containers
- **Build + deploy only** — no test suite in CI; tests run locally before pushing
- No Docker image tag strategy specified — Claude decides (latest or SHA)

### Cloudflare Pages deploy (React SPA)
- **GitHub integration** — Cloudflare Pages connected to the repo, auto-deploys on push to main
- API URL configured as **VITE_API_URL environment variable in Cloudflare Pages dashboard** — Vite bakes it into the build at deploy time
- **Default pages.dev subdomain** for now (no custom domain configured initially)
- **CORS: API allows all origins (wildcard)** — simplest for a small friend-group app

### Access control
- **Cloudflare Access** gates the entire pages.dev URL — random people cannot reach the login page
- Identity provider: **Email OTP** — approved email addresses receive a one-time passcode to pass the Cloudflare gate, then reach the JWT login page
- Cloudflare Access is configured in Cloudflare dashboard (free tier); email allow-list maintained there

### Claude's Discretion
- Docker image tagging strategy (latest vs SHA-based)
- Nginx worker_processes, connection limits, and header pass-through config
- GitHub Actions job structure (single job vs separate build/deploy jobs)
- Droplet SSH key setup and GitHub Actions secret names

</decisions>

<specifics>
## Specific Ideas

- The app is invite-only for a small friend group — security via Cloudflare Access (email OTP gate) + JWT auth is the right balance: two layers without complexity
- Cloudflare Access is free on the zero-trust free tier and requires no code changes — purely a Cloudflare dashboard configuration
- The DigitalOcean Droplet already decided in earlier planning: $12/month, 2GB RAM

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 05-production-deploy*
*Context gathered: 2026-03-06*
