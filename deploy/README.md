# Deploying Pantrio

```
Browser ──> Vercel (Next.js, https://<project>.vercel.app)
               │  server-side rewrite of /api/*
               ▼
            Caddy (edge, ports 80/443, automatic HTTPS) on the VPS
               ├──> pantrio-api:8080 ──> pantrio-db (PostgreSQL, private network only)
               └──> pawfectbite-server:8080 (existing app)
```

- The frontend runs on Vercel. The browser only talks to the Vercel origin, so auth cookies are first-party there.
- The API image is built by GitHub Actions (`.github/workflows/release.yml`) and pushed to `ghcr.io/michelleliuxing/pantrio-api`. The server only pulls images.
- All state lives in the `pantrio-pgdata` volume (data, and the cookie-encryption keys). The image plus a dump is enough to move hosts.

> Nothing in this runbook has been run against the VPS. Every server step needs the owner's go-ahead.

## What you need first

| Item | Where |
| --- | --- |
| An API hostname with an A record to the VPS, e.g. `api.pantrio.<your-domain>` | DNS. A subdomain of PawfectBite's domain works, or a free DuckDNS name |
| Vercel project | Import the GitHub repo, **Root Directory** `frontend`, env `API_ORIGIN=https://<api hostname>` |
| Google OAuth client (Web application) | Google Cloud Console. Redirect URI `https://<project>.vercel.app/api/auth/google/callback` (keep `http://localhost:3000/api/auth/google/callback` for dev) |
| OpenAI API key | OpenAI dashboard. Optional: without it the app uses the fake suggester |
| Pull access to the image | Make the `pantrio-api` package public in GitHub (Packages → Package settings), or `docker login ghcr.io` on the server with a token that has `read:packages` |
| A backup destination | Any rclone remote: Cloudflare R2, Backblaze B2, Google Drive, … |

## 0. Server preparation (owner tasks)

These protect both apps and are worth doing before anything else:

- Enable Vultr's automatic backups.
- SSH keys only: `PasswordAuthentication no` in `/etc/ssh/sshd_config`, then `sudo systemctl reload ssh`.
- Firewall: `sudo ufw allow 22/tcp && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw allow 443/udp && sudo ufw enable`.
- Swap (the box has 1 GB RAM and already runs a JVM and PostgreSQL):
  ```bash
  sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
  sudo sysctl vm.swappiness=10 && echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-swappiness.conf
  ```
- Check headroom: `free -m` and `docker stats --no-stream`. Pantrio adds about 100 MB in normal use (measured locally: API ~75 MB, PostgreSQL ~30 MB; both capped at 256 MB). If PawfectBite's JVM leaves less than ~250 MB free, resize the VPS or cap the JVM (`-Xmx`) first.

## 1. Start Pantrio (no change to PawfectBite yet)

```bash
sudo mkdir -p /opt/pantrio && sudo chown $USER /opt/pantrio
# copy deploy/compose.prod.yml and deploy/.env.prod.example from this repo to /opt/pantrio
cd /opt/pantrio
cp .env.prod.example .env && chmod 600 .env && nano .env     # fill in every value
docker network create edge
docker compose -f compose.prod.yml pull
docker compose -f compose.prod.yml up -d
docker logs pantrio-api | head -20                           # "Applying 1 migration(s)" then "Now listening"
```

The API is not reachable from outside yet: it has no published port, only the `edge` network.

## 2. Switch the edge from nginx to Caddy (short planned downtime for PawfectBite)

Caddy will own ports 80/443 for both apps and handle certificates, replacing PawfectBite's `nginx` and `certbot` containers and the certbot cron job.

1. Let Caddy reach PawfectBite's server. In PawfectBite's `docker-compose.prod.yml`, add the shared network to the `server` service (so it survives container re-creation):
   ```yaml
   services:
     server:
       networks: [default, edge]
   networks:
     edge:
       external: true
   ```
   then `docker compose -f docker-compose.prod.yml up -d server` (recreates the server only).
2. Prepare Caddy:
   ```bash
   sudo mkdir -p /opt/edge && sudo chown $USER /opt/edge
   # copy deploy/edge/compose.yml, deploy/edge/Caddyfile, deploy/edge/.env.example to /opt/edge
   cd /opt/edge && cp .env.example .env && nano .env   # PANTRIO_API_DOMAIN, PAWFECTBITE_DOMAIN, ACME_EMAIL
   docker compose config >/dev/null && echo config-ok
   ```
3. Cut over (about a minute of downtime for PawfectBite):
   ```bash
   docker compose -f /opt/pawfectbite/docker-compose.prod.yml stop nginx
   docker compose -f /opt/edge/compose.yml up -d
   docker logs -f edge-caddy        # watch both certificates being obtained, then Ctrl+C
   ```
4. Check: `curl -sI https://<pawfectbite domain>` and `curl -s https://<api hostname>/api/health` → `{"status":"ok"}`.
5. Remove the certbot renewal line from `crontab -e`, and the `nginx` and `certbot` services from PawfectBite's compose file once you are happy.

**Rollback:** `docker compose -f /opt/edge/compose.yml down && docker compose -f /opt/pawfectbite/docker-compose.prod.yml start nginx`. The old certificates are untouched in PawfectBite's `certbot/` folder.

## 3. Point Vercel at the API

1. In Vercel: Settings → Environment Variables → `API_ORIGIN=https://<api hostname>` (Production), then redeploy.
2. Make sure `PUBLIC_ORIGIN` in `/opt/pantrio/.env` is exactly the Vercel production URL, and that Google's redirect URI matches it. After editing `.env`: `docker compose -f /opt/pantrio/compose.prod.yml up -d`.
3. Open the Vercel URL on your phone and sign in with Google.

Vercel preview deployments use other hostnames, so Google sign-in only works on the production URL. That is expected.

## 4. Nightly backups and a tested restore

```bash
sudo apt install -y rclone          # or the latest from https://rclone.org/install/
rclone config                       # create a remote, e.g. "r2" for Cloudflare R2
mkdir -p /opt/pantrio/backup
# copy deploy/backup/backup.sh and restore.sh to /opt/pantrio/backup
cat > /opt/pantrio/backup.env <<'EOF'
BACKUP_REMOTE=r2:pantrio-backups
KEEP_DAYS=30
EOF
/opt/pantrio/backup/backup.sh                   # run once by hand
crontab -e                                      # add:
#   CRON_TZ=Australia/Sydney
#   30 2 * * * /opt/pantrio/backup/backup.sh >> /var/log/pantrio-backup.log 2>&1
```

Test a restore once, into a scratch container, never the live one:

```bash
docker run -d --name pantrio-restore-test -e POSTGRES_USER=pantrio -e POSTGRES_PASSWORD=x -e POSTGRES_DB=pantrio postgres:18-alpine
sleep 5
/opt/pantrio/backup/restore.sh "r2:pantrio-backups/<latest dump>" pantrio-restore-test   # prints row counts
docker exec pantrio-db psql -U pantrio -d pantrio -c "select count(*) from recipe"       # compare with the live database
docker rm -f pantrio-restore-test
```

The same script restores into the live database (`restore.sh <dump>` with no container argument; it asks for confirmation). Stop the API first.

## Updating

Every green push to `main` publishes `ghcr.io/michelleliuxing/pantrio-api:<short sha>` and `:latest`.

```bash
cd /opt/pantrio
# optional: pin the exact build in .env, PANTRIO_API_IMAGE=ghcr.io/michelleliuxing/pantrio-api:<sha>
docker compose -f compose.prod.yml pull pantrio-api && docker compose -f compose.prod.yml up -d pantrio-api
```

Migrations run when the API starts. Roll back by pinning the previous tag (and restoring the pre-update dump if a migration changed data).

## Moving to a bigger server or managed PostgreSQL

1. Take a dump with `backup.sh` (or `pg_dump -Fc`).
2. On the new host: copy `/opt/pantrio` and `/opt/edge`, start `pantrio-db`, run `restore.sh <dump>`, start the API.
3. For managed PostgreSQL: drop the `pantrio-db` service, set `ConnectionStrings__Pantrio` to the managed instance, and `pg_restore` the dump into it.
4. Move the DNS record for the API hostname.
