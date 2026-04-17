#!/usr/bin/env bash
set -euo pipefail

# Code-only KnightFlix deploy: builds a new image from /root/jellyfin-pi-build.tar.gz
# and recreates the KnightFlix container with the same bind mounts so data persists.
#
# Usage on Pi:
#   bash deploy_knightflix_codeonly.sh knightflix:2026-04-12-1530

IMAGE_TAG="${1:-}"
if [[ -z "${IMAGE_TAG}" ]]; then
  echo "ERROR: missing IMAGE_TAG argument (example: knightflix:2026-04-12-1530)" >&2
  exit 2
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker not found" >&2
  exit 1
fi

if ! docker inspect KnightFlix >/dev/null 2>&1; then
  echo "ERROR: container 'KnightFlix' not found" >&2
  exit 1
fi

TAR_PATH="/root/jellyfin-pi-build.tar.gz"
if [[ ! -f "${TAR_PATH}" ]]; then
  echo "ERROR: build tar not found at ${TAR_PATH}" >&2
  exit 1
fi

safe_tag="${IMAGE_TAG//[:\/]/_}"
WORKDIR="/root/knightflix-build-${safe_tag}"

echo "=== Build Image: ${IMAGE_TAG} ==="
rm -rf "${WORKDIR}"
mkdir -p "${WORKDIR}"
tar -xzf "${TAR_PATH}" -C "${WORKDIR}"
DOCKER_BUILDKIT=0 docker build -t "${IMAGE_TAG}" -f "${WORKDIR}/Dockerfile.pi" "${WORKDIR}"

echo "=== Resolve Existing Bind Mounts (data safety) ==="
cfg="$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/config"}}{{.Source}}{{end}}{{end}}')"
cache="$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/cache"}}{{.Source}}{{end}}{{end}}')"
media1="$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/media1"}}{{.Source}}{{end}}{{end}}')"
media2="$(docker inspect KnightFlix --format '{{range .Mounts}}{{if eq .Destination "/media2"}}{{.Source}}{{end}}{{end}}')"
tz="$(docker inspect KnightFlix --format '{{range .Config.Env}}{{println .}}{{end}}' | grep '^TZ=' | head -n 1 | cut -d= -f2- || true)"
if [[ -z "${tz}" ]]; then tz="Asia/Kolkata"; fi

echo "MOUNT /config => ${cfg}"
echo "MOUNT /cache  => ${cache}"
echo "MOUNT /media1 => ${media1}"
echo "MOUNT /media2 => ${media2}"
echo "TZ=${tz}"

if [[ -z "${cfg}" || -z "${cache}" || -z "${media1}" || -z "${media2}" ]]; then
  echo "ERROR: one or more required mounts could not be resolved; refusing to deploy." >&2
  exit 1
fi

if [[ "${DO_DB_BACKUP:-0}" == "1" ]]; then
  echo "=== Backup jellyfin.db (requested) ==="
  ts="$(date +%Y%m%d-%H%M%S)"
  mkdir -p "${cfg}/data/SQLiteBackups"
  backup_path="${cfg}/data/SQLiteBackups/jellyfin-preupgrade-${ts}.db"

  # Prefer sqlite .backup via helper container; if unavailable, stop container and copy DB file.
  if docker image inspect keinos/sqlite3:latest >/dev/null 2>&1 || docker pull keinos/sqlite3:latest >/dev/null 2>&1; then
    docker run --rm --user root -v "${cfg}/data:/data" keinos/sqlite3:latest \
      sqlite3 /data/jellyfin.db ".backup '${backup_path}'"
  else
    echo "WARN: couldn't pull keinos/sqlite3; stopping container and copying DB file." >&2
    docker stop KnightFlix >/dev/null 2>&1 || true
    cp -f "${cfg}/data/jellyfin.db" "${backup_path}"
  fi
  echo "DB_BACKUP=${backup_path}"
else
  echo "=== DB Backup Skipped (DO_DB_BACKUP=1 to enable) ==="
fi

echo "=== Recreate Container With New Image (bind mounts unchanged) ==="
docker rm -f KnightFlix >/dev/null 2>&1 || true
docker run -d --name KnightFlix --restart unless-stopped \
  -p 8097:8096 -p 8921:8920 \
  -e "TZ=${tz}" \
  -v "${cfg}:/config" -v "${cache}:/cache" -v "${media1}:/media1" -v "${media2}:/media2" \
  "${IMAGE_TAG}" dotnet /opt/jellyfin/jellyfin.dll --datadir /config --cachedir /cache

echo "=== Verify ==="
sleep 4
docker ps --format '{{.Names}} {{.Image}} {{.Ports}}' | grep KnightFlix || true

if command -v curl >/dev/null 2>&1; then
  curl -fsS "http://127.0.0.1:8097/System/Info/Public" >/dev/null
  echo "HEALTH=OK"
elif command -v wget >/dev/null 2>&1; then
  wget -qO- "http://127.0.0.1:8097/System/Info/Public" >/dev/null
  echo "HEALTH=OK"
else
  echo "WARN: neither curl nor wget available to verify HTTP health" >&2
fi

echo "DONE"
