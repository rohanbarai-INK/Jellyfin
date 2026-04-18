# Pi Code-Only Deploy (KnightFlix) - Repeatable Runbook

Goal: deploy new Jellyfin server + web code to the live `KnightFlix` container on the Pi **without touching user data or metadata**.

Data safety rule: **never change or delete the host `/config` path**. It contains the DB (`jellyfin.db`), users, and metadata.

This runbook assumes:
- Pi SSH: `root@192.168.1.7`
- Host mounts (from `docker inspect KnightFlix`):
  - `/srv/dockerdata/knightflix/config => /config`
  - `/var/cache/knightflix => /cache`
  - `/srv/dev-disk-by-uuid-7b2260f5-9928-4ef0-a7db-5802e2b023c7 => /media1`
  - `/srv/dev-disk-by-uuid-4de857dc-2d58-4ecd-a473-02e1c265c87f/MediaServer => /media2`
  - Optional docker volume for `/media`

---

## 0) Rebuild Web UI (Windows)

```pwsh
cd "C:\Users\Barai Brothers\Documents\Jellyfin\jellyfin-web"
 
# Rebuild production bundle
npm run build:production
 
# Verify dist exists
Test-Path .\dist\index.html
```

## 1) Create a Pi Build Tar (Windows)

Run from repo root:

```pwsh
cd "C:\Users\Barai Brothers\Documents\Jellyfin"

# Must exist: we ship prebuilt web UI to the Pi so the Pi doesn't run webpack.
Test-Path .\jellyfin-web\dist\index.html

# Stage build context (includes jellyfin + jellyfin-web + prebuilt dist)
Remove-Item -Recurse -Force .run\pi-build-context -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force .run\pi-build-context | Out-Null

Copy-Item -Force Dockerfile.pi .run\pi-build-context\Dockerfile.pi
Copy-Item -Force .dockerignore .run\pi-build-context\.dockerignore

# Copy server + web sources. Keep dist, exclude node_modules/bin/obj and nested .run artifacts.
robocopy jellyfin .run\pi-build-context\jellyfin /E /XD bin obj .run .tmp-run /NFL /NDL /NJH /NJS /NP
robocopy jellyfin-web .run\pi-build-context\jellyfin-web /E /XD node_modules bin obj /NFL /NDL /NJH /NJS /NP

# Build tar (overwrite any old one)
Remove-Item -Force .\jellyfin-pi-build.tar.gz -ErrorAction SilentlyContinue
tar -czf .\jellyfin-pi-build.tar.gz -C .run\pi-build-context Dockerfile.pi .dockerignore jellyfin jellyfin-web

# Sanity check size (should be hundreds of MB)
Get-Item .\jellyfin-pi-build.tar.gz | Select FullName,Length,LastWriteTime
```

Upload to Pi:
```pwsh
scp .\jellyfin-pi-build.tar.gz root@192.168.1.7:/root/jellyfin-pi-build.tar.gz
```

---

## 2) Build the New Image on the Pi (Tabby Terminal)

SSH in:
```bash
ssh root@192.168.1.7
```

Confirm the live mounts (authoritative):
```bash
docker inspect KnightFlix --format '{{range .Mounts}}{{println .Source "=>" .Destination}}{{end}}'
```

Confirm tar exists:
```bash
ls -lh /root/jellyfin-pi-build.tar.gz
```

Create new tag and build:
```bash
IMAGE_TAG="knightflix:$(date +%Y-%m-%d-%H%M)"
echo "IMAGE_TAG=$IMAGE_TAG"

WORKDIR="/root/knightflix-build-${IMAGE_TAG//[:\/]/_}"
rm -rf "$WORKDIR"
mkdir -p "$WORKDIR"
tar -xzf /root/jellyfin-pi-build.tar.gz -C "$WORKDIR"

# Must exist in build context:
ls -lh "$WORKDIR/jellyfin-web/dist/index.html"

DOCKER_BUILDKIT=0 docker build -t "$IMAGE_TAG" -f "$WORKDIR/Dockerfile.pi" "$WORKDIR"
```

If the build fails with "no space left":
```bash
docker builder prune -af
docker image prune -af
docker system prune -af
df -h
```

---

## 3) Recreate KnightFlix Container (Data-Safe)

Important: our image already has an ENTRYPOINT with `--datadir /config --cachedir /cache`.
If you pass those again, Jellyfin errors with "Option datadir/cachedir defined multiple times".

Use this safe recreate command (forces a clean entrypoint so args appear once):

```bash
cfg="/srv/dockerdata/knightflix/config"
cache="/var/cache/knightflix"
m1="/srv/dev-disk-by-uuid-7b2260f5-9928-4ef0-a7db-5802e2b023c7"
m2="/srv/dev-disk-by-uuid-4de857dc-2d58-4ecd-a473-02e1c265c87f/MediaServer"
tz="Asia/Kolkata"

# Optional: keep old /media docker volume if it exists
mediaVol="$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/media"}}{{.Name}}{{end}}{{end}}' 2>/dev/null || true)"

docker rm -f KnightFlix 2>/dev/null || true

if [ -n "$mediaVol" ]; then
  docker run -d --name KnightFlix --restart unless-stopped \
    -p 8097:8096 -p 8921:8920 \
    -e "TZ=$tz" \
    -v "$cfg:/config" -v "$cache:/cache" -v "$mediaVol:/media" -v "$m1:/media1" -v "$m2:/media2" \
    --entrypoint dotnet \
    "$IMAGE_TAG" /opt/jellyfin/jellyfin.dll --datadir /config --cachedir /cache
else
  docker run -d --name KnightFlix --restart unless-stopped \
    -p 8097:8096 -p 8921:8920 \
    -e "TZ=$tz" \
    -v "$cfg:/config" -v "$cache:/cache" -v "$m1:/media1" -v "$m2:/media2" \
    --entrypoint dotnet \
    "$IMAGE_TAG" /opt/jellyfin/jellyfin.dll --datadir /config --cachedir /cache
fi
```

---

## 4) Verify "Deployment Complete"

```bash
docker ps --filter name=KnightFlix --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}'
docker logs --tail 80 KnightFlix
```

Health check (wait until logs show `Main: Startup complete ...`):
```bash
i=0
until curl -fsS http://127.0.0.1:8097/System/Info/Public >/dev/null; do
  i=$((i+1))
  echo "not ready yet ($i)"; sleep 3
  [ $i -ge 40 ] && echo "still not ready, check logs" && break
done
curl -fsS http://127.0.0.1:8097/System/Info/Public | head -c 200; echo
```

Open UI:
- `http://192.168.1.7:8097/web/`

---

## 5) Cleanup (Optional)

## Remove the tar to free space:

rm -f /root/jellyfin-pi-build.tar.gz


## Remove temp build folders:
rm -rf /root/knightflix-build-*


## Prune Docker caches (safe for `/config` bind mount):

docker builder prune -af
docker image prune -af


