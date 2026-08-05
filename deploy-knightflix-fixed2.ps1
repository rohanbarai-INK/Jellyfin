param(
    [string]$RepoRoot = 'C:\Users\Barai Brothers\Documents\Jellyfin',
    [string]$PiHost = '192.168.1.7',
    [string]$PiUser = 'root',
    [string]$RemoteTar = '/tmp/jellyfin-pi-build.tar.gz',
    [int]$RemoteCommandTimeoutSec = 7200,
    [int]$CleanupTimeoutSec = 900,
    [switch]$SkipCleanup
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Assert-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Gray
}

Write-Step 'Checking local prerequisites'
Assert-Command npm
Assert-Command robocopy
Assert-Command tar

if (-not (Test-Path $RepoRoot)) {
    throw "Repo root not found: $RepoRoot"
}

$webRoot = Join-Path $RepoRoot 'jellyfin-web'
$distIndex = Join-Path $webRoot 'dist\index.html'
$runRoot = Join-Path $RepoRoot '.run'
$buildContext = Join-Path $runRoot 'pi-build-context'
$tarPath = Join-Path $RepoRoot 'jellyfin-pi-build.tar.gz'
$dockerfilePi = Join-Path $RepoRoot 'Dockerfile.pi'
$dockerIgnore = Join-Path $RepoRoot '.dockerignore'
$jellyfinSrc = Join-Path $RepoRoot 'jellyfin'
$jellyfinWebSrc = Join-Path $RepoRoot 'jellyfin-web'

if (-not (Test-Path $dockerfilePi)) { throw "Missing Dockerfile.pi at $dockerfilePi" }
if (-not (Test-Path $dockerIgnore)) { throw "Missing .dockerignore at $dockerIgnore" }
if (-not (Test-Path $jellyfinSrc)) { throw "Missing jellyfin source folder at $jellyfinSrc" }
if (-not (Test-Path $jellyfinWebSrc)) { throw "Missing jellyfin-web source folder at $jellyfinWebSrc" }

Write-Step 'Installing/importing Posh-SSH if needed'
if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
    Install-Module -Name Posh-SSH -Scope CurrentUser -Force -AllowClobber
}
Import-Module Posh-SSH -Force

Write-Step 'Prompting once for Pi password'
$securePassword = Read-Host -AsSecureString "Enter SSH password for $PiUser@$PiHost"
$credential = [pscredential]::new($PiUser, $securePassword)

Write-Step 'Rebuilding production web UI'
Push-Location $webRoot
try {
    npm run build:production
}
finally {
    Pop-Location
}

if (-not (Test-Path $distIndex)) {
    throw "Web build did not produce $distIndex"
}

Write-Step 'Creating build context'
if (Test-Path $buildContext) {
    Remove-Item -Recurse -Force $buildContext
}
New-Item -ItemType Directory -Force -Path $buildContext | Out-Null

Copy-Item -Force $dockerfilePi (Join-Path $buildContext 'Dockerfile.pi')
Copy-Item -Force $dockerIgnore (Join-Path $buildContext '.dockerignore')

$null = robocopy $jellyfinSrc (Join-Path $buildContext 'jellyfin') /E /XD bin obj .run .tmp-run /NFL /NDL /NJH /NJS /NP
$null = robocopy $jellyfinWebSrc (Join-Path $buildContext 'jellyfin-web') /E /XD node_modules bin obj /NFL /NDL /NJH /NJS /NP

if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed while staging files. Exit code: $LASTEXITCODE"
}

Write-Step 'Building tarball'
if (Test-Path $tarPath) {
    Remove-Item -Force $tarPath
}

Push-Location $RepoRoot
try {
    & tar -czf $tarPath -C $buildContext Dockerfile.pi .dockerignore jellyfin jellyfin-web
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $tarPath)) {
    throw "Tarball was not created: $tarPath"
}

Get-Item $tarPath | Select-Object FullName, Length, LastWriteTime | Format-List

$remoteTarDir = ($RemoteTar -replace '/[^/]+$','')
if (-not $remoteTarDir -or $remoteTarDir -eq $RemoteTar) {
    throw "Could not determine remote tar directory from Linux path: $RemoteTar"
}
if (-not $remoteTarDir.StartsWith('/')) {
    throw "Remote tar directory must be an absolute Linux path. Got: $remoteTarDir"
}

$localDeployScript = Join-Path $env:TEMP 'deploy_knightflix_remote.sh'
$localCleanupScript = Join-Path $env:TEMP 'cleanup_knightflix_remote.sh'
$remoteDeployScript = '/tmp/deploy_knightflix_remote.sh'
$remoteCleanupScript = '/tmp/cleanup_knightflix_remote.sh'

$remoteScript = @'
set -euo pipefail

IMAGE_TAG="knightflix:$(date +%Y-%m-%d-%H%M)"
echo "IMAGE_TAG=$IMAGE_TAG"

cfg="/srv/dockerdata/knightflix/config"
cache="/var/cache/knightflix"
m1="/srv/dev-disk-by-uuid-7b2260f5-9928-4ef0-a7db-5802e2b023c7"
m2="/srv/dev-disk-by-uuid-4de857dc-2d58-4ecd-a473-02e1c265c87f/MediaServer"
tz="Asia/Kolkata"

if [ ! -f '__REMOTE_TAR__' ]; then
  echo "Missing uploaded tar: __REMOTE_TAR__" >&2
  exit 1
fi

WORKDIR="/root/knightflix-build-${IMAGE_TAG//[:\/]/_}"
rm -rf "$WORKDIR"
mkdir -p "$WORKDIR"
tar -xzf '__REMOTE_TAR__' -C "$WORKDIR"

if [ ! -f "$WORKDIR/jellyfin-web/dist/index.html" ]; then
  echo "Missing prebuilt web UI: $WORKDIR/jellyfin-web/dist/index.html" >&2
  exit 1
fi

echo "Current live mounts:"
docker inspect KnightFlix --format '{{range .Mounts}}{{println .Source "=>" .Destination}}{{end}}' || true

if ! DOCKER_BUILDKIT=0 docker build -t "$IMAGE_TAG" -f "$WORKDIR/Dockerfile.pi" "$WORKDIR"; then
  echo "Initial docker build failed. Pruning docker caches and retrying once..."
  docker builder prune -af || true
  docker image prune -af || true
  docker system prune -af || true
  df -h || true
  DOCKER_BUILDKIT=0 docker build -t "$IMAGE_TAG" -f "$WORKDIR/Dockerfile.pi" "$WORKDIR"
fi

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

echo "\\nContainer status:"
docker ps --filter name=KnightFlix --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}'

echo "\\nRecent logs:"
docker logs --tail 80 KnightFlix || true

echo "\\nMedia mount sanity check (/media2):"
if docker exec KnightFlix sh -lc 'ls -1 /media2 >/dev/null 2>&1'; then
  docker exec KnightFlix sh -lc 'ls -1 /media2 | head -n 10' || true
else
  echo "WARN: /media2 is not readable inside container. Restarting container to rebind mounts..."
  docker restart KnightFlix
  sleep 3
  docker exec KnightFlix sh -lc 'ls -1 /media2 | head -n 10'
fi

echo "\\nWaiting for health endpoint..."
i=0
until curl -fsS http://127.0.0.1:8097/System/Info/Public >/dev/null; do
  i=$((i+1))
  echo "not ready yet ($i)"
  sleep 3
  if [ $i -ge 40 ]; then
    echo "still not ready, check logs" >&2
    exit 1
  fi
done

curl -fsS http://127.0.0.1:8097/System/Info/Public | head -c 200; echo

echo "\\nDeployment complete. Open: http://192.168.1.7:8097/web/"
'@

$cleanupScript = @'
set -euo pipefail
rm -f '__REMOTE_TAR__'
rm -rf /root/knightflix-build-*
rm -f /tmp/deploy_knightflix_remote.sh /tmp/cleanup_knightflix_remote.sh
docker builder prune -af || true
docker image prune -af || true
echo "Cleanup finished"
'@

$deployContent = $remoteScript.Replace('__REMOTE_TAR__', $RemoteTar).Replace("`r`n", "`n")
$cleanupContent = $cleanupScript.Replace('__REMOTE_TAR__', $RemoteTar).Replace("`r`n", "`n")
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($localDeployScript, $deployContent, $utf8NoBom)
[System.IO.File]::WriteAllText($localCleanupScript, $cleanupContent, $utf8NoBom)

Write-Step 'Opening SSH session'
$sshSession = New-SSHSession -ComputerName $PiHost -Credential $credential -AcceptKey
try {
    $sessionId = $sshSession.SessionId

    Write-Step 'Uploading tarball to Pi'
    Set-SCPItem -ComputerName $PiHost -Credential $credential -AcceptKey -Path $tarPath -Destination $remoteTarDir

    Write-Step 'Uploading remote deploy script'
    Set-SCPItem -ComputerName $PiHost -Credential $credential -AcceptKey -Path $localDeployScript -Destination '/tmp'
    Set-SCPItem -ComputerName $PiHost -Credential $credential -AcceptKey -Path $localCleanupScript -Destination '/tmp'

    Write-Step 'Running remote build + deploy'
    Write-Info "Allowing up to $RemoteCommandTimeoutSec seconds for the remote build/deploy step"
    $result = Invoke-SSHCommand -SessionId $sessionId -Command "sed -i 's/\r$//' $remoteDeployScript && chmod +x $remoteDeployScript && bash $remoteDeployScript" -TimeOut $RemoteCommandTimeoutSec

    if ($result.Output) {
        $result.Output | ForEach-Object { Write-Host $_ }
    }
    if ($result.Error) {
        $result.Error | ForEach-Object { Write-Warning $_ }
    }
    if ($result.ExitStatus -ne 0) {
        throw "Remote deployment failed with exit status $($result.ExitStatus)"
    }

    if (-not $SkipCleanup) {
        Write-Step 'Running remote cleanup'
        Write-Info "Allowing up to $CleanupTimeoutSec seconds for the remote cleanup step"
        $cleanupResult = Invoke-SSHCommand -SessionId $sessionId -Command "sed -i 's/\r$//' $remoteCleanupScript && chmod +x $remoteCleanupScript && bash $remoteCleanupScript" -TimeOut $CleanupTimeoutSec
        if ($cleanupResult.Output) {
            $cleanupResult.Output | ForEach-Object { Write-Host $_ }
        }
        if ($cleanupResult.Error) {
            $cleanupResult.Error | ForEach-Object { Write-Warning $_ }
        }
        if ($cleanupResult.ExitStatus -ne 0) {
            throw "Remote cleanup failed with exit status $($cleanupResult.ExitStatus)"
        }
    }

    Write-Step 'Finished'
    Write-Host 'Deployment succeeded.' -ForegroundColor Green
}
finally {
    Remove-Item -Force $localDeployScript -ErrorAction SilentlyContinue
    Remove-Item -Force $localCleanupScript -ErrorAction SilentlyContinue
    if ($sshSession) {
        Remove-SSHSession -SessionId $sshSession.SessionId | Out-Null
    }
}
