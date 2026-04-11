# KnightFlix (Jellyfin) Pi5 Build + Deploy Runbook

This is a reusable runbook to (1) build a Pi5-compatible Jellyfin Server+Web Docker image from this repo and (2) deploy/update the `KnightFlix` stack on a Raspberry Pi while keeping existing KnightFlix data intact (users, profiles, metadata, libraries).

Do not put real credentials in this file. Fill the placeholders at runtime.

## Variables to fill (runtime)
- `PI_IP` = `192.168.1.7`
- `PI_USER` = `root` (or `__FILL__`)
- `PI_PASS` =  `prnrr123`
- `IMAGE_TAG` = `knightflix:local` (or versioned like `knightflix:2026-04-11`)

Pi paths (keep same to preserve data):
- `KNIGHTFLIX_CONFIG_DIR` = `__AUTO_FROM_CONTAINER__` (do not hardcode; detect from `docker inspect KnightFlix`)
- `KNIGHTFLIX_CACHE_DIR` = `/var/cache/knightflix`
- `MEDIA1_DIR` = `/srv/dev-disk-by-uuid-7b2260f5-9928-4ef0-a7db-5802e2b023c7`
- `MEDIA2_DIR` = `/srv/dev-disk-by-uuid-4de857dc-2d58-4ecd-a473-02e1c265c87f/MediaServer`

Live-path check (authoritative source):
```bash
docker inspect KnightFlix --format '{{range .Mounts}}{{println .Source "=>" .Destination}}{{end}}'
```
Pick the source that maps to `/config` as `KNIGHTFLIX_CONFIG_DIR`.

Repo files we use:
- `Dockerfile.pi`
- `.dockerignore`
- `KnightFlix.stack.yml`

## 0) SSH (Windows, non-interactive)
We use PowerShell + Posh-SSH so we can pass `PI_IP`/`PI_PASS` dynamically.

Install once:
```pwsh
Install-Module -Name Posh-SSH -Scope CurrentUser -Force -AllowClobber
```

Template (use in scripts, do not store password):
```pwsh
$PI_IP="__FILL__"
$PI_USER="root"
$PI_PASS="__FILL__"
$cred = New-Object PSCredential($PI_USER,(ConvertTo-SecureString $PI_PASS -AsPlainText -Force))
$sess = New-SSHSession -ComputerName $PI_IP -Credential $cred -AcceptKey -ConnectionTimeout 10
# Invoke-SSHCommand -SessionId $sess.SessionId -Command "..."
# Remove-SSHSession -SessionId $sess.SessionId
```

## 1) Create build-context tar (Server + Web)
Run on your workstation from repo root (`C:\Users\Barai Brothers\Documents\Jellyfin`):
```pwsh
tar -czf jellyfin-pi-build.tar.gz `
  Dockerfile.pi .dockerignore jellyfin jellyfin-web `
  --exclude "jellyfin-web/node_modules" `
  --exclude "jellyfin-web/dist" `
  --exclude "**/bin" --exclude "**/obj"
```

## 2) First-time deploy (new Pi / new KnightFlix)
### 2.1 Build image on the Pi (Portainer recommended)
- Portainer -> Images -> Build a new image
- Upload `jellyfin-pi-build.tar.gz`
- Dockerfile: `Dockerfile.pi`
- Tag: `IMAGE_TAG` (example: `knightflix:local`)

CLI pre-step (if building via SSH):
```pwsh
scp jellyfin-pi-build.tar.gz ${PI_USER}@${PI_IP}:/root/
```

CLI alternative (SSH):
```bash
mkdir -p /root/knightflix-build
tar -xzf /root/jellyfin-pi-build.tar.gz -C /root/knightflix-build
DOCKER_BUILDKIT=0 docker build -t IMAGE_TAG -f /root/knightflix-build/Dockerfile.pi /root/knightflix-build
```

Cleanup (optional, after the image is built successfully):
```bash
rm -f /root/jellyfin-pi-build.tar.gz
rm -rf /root/knightflix-build
```

### 2.2 Deploy the stack
Use the same compose we already used on the Pi: `KnightFlix.stack.yml`.

Key properties (do not change if you want data persistence):
- `/config` bind mount points to `KNIGHTFLIX_CONFIG_DIR`
- `/cache` bind mount points to `KNIGHTFLIX_CACHE_DIR`
- media mounts match `MEDIA1_DIR` and `MEDIA2_DIR`

`KnightFlix.stack.yml` (reference):
```yaml
version: "3.9"
services:
  KnightFlix:
    image: IMAGE_TAG
    container_name: KnightFlix
    pull_policy: never
    entrypoint:
      - dotnet
      - /opt/jellyfin/jellyfin.dll
      - --datadir
      - /config
      - --cachedir
      - /cache
    ports:
      - "8097:8096"
      - "8921:8920"
    environment:
      - TZ=Asia/Kolkata
    volumes:
      - KNIGHTFLIX_CONFIG_DIR:/config
      - KNIGHTFLIX_CACHE_DIR:/cache
      - MEDIA1_DIR:/media1
      - MEDIA2_DIR:/media2
    restart: unless-stopped
```

Deploy:
- Portainer -> Stacks -> Add stack -> paste the YAML (or open existing stack and update) -> Deploy

### 2.3 Verify
SSH commands:
```bash
docker ps --format '{{.Names}} {{.Ports}}'
curl -I http://127.0.0.1:8097/ | head -n 5
docker exec KnightFlix ls -l /media2 | head
```

## 3) Deploy a NEW version (keep KnightFlix data intact)
Goal: upgrade code/image ONLY, keeping existing KnightFlix users/profiles/metadata/libraries.

### 3.1 Backup KnightFlix DB (recommended)
First resolve config dir from running container:
```bash
KNIGHTFLIX_CONFIG_DIR=$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/config"}}{{.Source}}{{end}}{{end}}')
echo "$KNIGHTFLIX_CONFIG_DIR"
```

Use a sqlite helper container so no host sqlite install is required:
```bash
sqlimg=keinos/sqlite3:latest
docker image inspect $sqlimg >/dev/null 2>&1 || docker pull $sqlimg
docker run --rm --user root -v "KNIGHTFLIX_CONFIG_DIR/data:/data" $sqlimg \
  sqlite3 /data/jellyfin.db ".backup '/data/SQLiteBackups/jellyfin-preupgrade.db'"
```

### 3.2 Build the new image
- Make your code changes in this repo
- Recreate `jellyfin-pi-build.tar.gz` (section 1)
- Build a new image tag on the Pi (section 2.1)

Recommendation: use a versioned tag so Portainer/compose will recreate the container:
- Example: `knightflix:2026-04-11`

### 3.3 Update stack image + redeploy (recreate container)
Important: a plain `docker restart` keeps the old image (container pins an image ID). You must redeploy/recreate.

Portainer:
- Stacks -> `KnightFlix` -> edit `image:` to the new `IMAGE_TAG` -> Deploy (this recreates the container)

CLI alternative:
```bash
docker compose -p knightflix -f /path/to/stack.yml up -d --force-recreate
```

Data safety: because `/config` and `/cache` are bind mounts, recreating the container does not delete your users/metadata.

### 3.4 Post-upgrade quick checks
```bash
docker logs --tail 50 KnightFlix
docker exec KnightFlix ls -lh /config/data/jellyfin.db
docker exec KnightFlix ls -l /media2/Hollywood | head
```

## 4) Common issues + fixes
### 4.1 Setup wizard shows on 8097
Root cause: KnightFlix is using a fresh tiny DB at `/config/data/jellyfin.db` (users missing).

Check:
```bash
docker exec KnightFlix ls -lh /config/data/jellyfin.db
```
Fix (restore the correct DB into `KNIGHTFLIX_CONFIG_DIR/data/jellyfin.db`, then recreate container).

### 4.2 Media path exists on host but missing in container
Symptom: logs show `Could not find file '/media2/...mp4'` and ffmpeg exits with code `254`.
Fix: restart the container so Docker rebinds the mount:
```bash
docker restart KnightFlix
docker exec KnightFlix ls -l /media2 | head
```

### 4.3 Root disk fills up
Main cause: transcodes under `KNIGHTFLIX_CACHE_DIR/transcodes`.
Safe cleanup:
```bash
rm -rf KNIGHTFLIX_CACHE_DIR/transcodes/*
```

## 5) Optional one-off DB operations (only if you want them)
### 5.1 Set all non-admin users to 12-month active
```bash
sqlimg=keinos/sqlite3:latest
docker run --rm --user root -v "KNIGHTFLIX_CONFIG_DIR/data:/data" $sqlimg sqlite3 /data/jellyfin.db \
  "WITH admins AS (SELECT UserId FROM Permissions WHERE Kind=0 AND Value=1)
   UPDATE Users SET ExpiryDate=datetime('now','+12 months')
   WHERE Id NOT IN (SELECT UserId FROM admins);"
```

### 5.2 Reset watch/time-spent (achievements reset)
```bash
sqlimg=keinos/sqlite3:latest
docker run --rm --user root -v "KNIGHTFLIX_CONFIG_DIR/data:/data" $sqlimg sqlite3 /data/jellyfin.db \
  "DELETE FROM UserWatchSessions;
   DELETE FROM UserBingeSessions;
   DELETE FROM UserPeriodStats;
   DELETE FROM UserPeriodHourlyStats;
   DELETE FROM UserGenrePeriodStats;
   UPDATE UserData SET PlaybackPositionTicks=0;
   UPDATE UserData SET PlayCount=0;
   UPDATE UserData SET Played=0;
   UPDATE UserData SET LastPlayedDate=NULL;"
```
