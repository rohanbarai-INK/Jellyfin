param(
    [string]$RepoRoot = 'C:\Users\Barai Brothers\Documents\Jellyfin',
    [string]$PiHost = '192.168.1.7',
    [string]$PiUser = 'root',
    [string]$RemoteTar = '/root/jellyfin-pi-build.tar.gz',
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
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
} catch {}

if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
    Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
}

try {
    Set-PSRepository -Name PSGallery -InstallationPolicy Trusted
} catch {}

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

Write-Step 'Opening SSH session'
$sshSession = New-SSHSession -ComputerName $PiHost -Credential $credential -AcceptKey
try {
    $sessionId = $sshSession.SessionId

    Write-Step 'Uploading tarball to Pi'
    $remoteTarDir = [System.IO.Path]::GetDirectoryName($RemoteTar).Replace('\','/')
    Set-SCPItem -ComputerName $PiHost -Credential $credential -AcceptKey -Path $tarPath -Destination $remoteTarDir

    Write-Step 'Running remote build + deploy'
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

    $remoteScript = $remoteScript.Replace('__REMOTE_TAR__', $RemoteTar)
    $remoteEncoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($remoteScript))
    $command = "bash -lc 'printf %s $remoteEncoded | base64 -d > /tmp/deploy_knightflix.sh && chmod +x /tmp/deploy_knightflix.sh && /tmp/deploy_knightflix.sh'"
    $result = Invoke-SSHCommand -SessionId $sessionId -Command $command -TimeOut 0

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
        $cleanup = @'
set -euo pipefail
rm -f '__REMOTE_TAR__'
rm -rf /root/knightflix-build-*
docker builder prune -af || true
docker image prune -af || true
echo "Cleanup finished"
'@
        $cleanup = $cleanup.Replace('__REMOTE_TAR__', $RemoteTar)
        $cleanupEncoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cleanup))
        $cleanupCommand = "bash -lc 'printf %s $cleanupEncoded | base64 -d > /tmp/cleanup_knightflix.sh && chmod +x /tmp/cleanup_knightflix.sh && /tmp/cleanup_knightflix.sh'"
        $cleanupResult = Invoke-SSHCommand -SessionId $sessionId -Command $cleanupCommand -TimeOut 0
        if ($cleanupResult.Output) {
            $cleanupResult.Output | ForEach-Object { Write-Host $_ }
        }
    }

    Write-Step 'Finished'
    Write-Host 'Deployment succeeded.' -ForegroundColor Green
}
finally {
    if ($sshSession) {
        Remove-SSHSession -SessionId $sshSession.SessionId | Out-Null
    }
}
