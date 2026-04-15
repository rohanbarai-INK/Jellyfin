@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM One-click Pi deploy for KnightFlix (code-only) with realtime stage logs.
REM Uses PuTTY plink/pscp for password-based SSH/SCP.
REM Password is prompted at runtime (not stored in file).

set "ROOT=%~dp0"
pushd "%ROOT%" >nul

set "PI_HOST=192.168.1.7"
set "PI_USER=root"
set "PI_TAR_REMOTE=/root/jellyfin-pi-build.tar.gz"
set "PI_CONTAINER=KnightFlix"

set "PLINK=%ROOT%plink.exe"
set "PSCP=%ROOT%pscp.exe"

if not exist "%PLINK%" (
  echo [ERROR] plink.exe not found at "%PLINK%"
  echo         Put plink.exe in repo root and re-run.
  exit /b 1
)

REM Prompt for password securely using PowerShell
for /f "delims=" %%P in ('powershell -Command "$p = Read-Host 'Enter SSH password for %PI_USER%@%PI_HOST%' -AsSecureString; $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($p); $Plain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR); [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR); Write-Output $Plain"') do set "PI_PASS=%%P"
if "%PI_PASS%"=="" (
  echo [ERROR] Password cannot be empty.
  exit /b 1
)

echo [5%%] Checking local prerequisites...
where npm >nul 2>nul || (echo [ERROR] npm not found in PATH & exit /b 1)
where tar >nul 2>nul || (echo [ERROR] tar not found in PATH & exit /b 1)
where robocopy >nul 2>nul || (echo [ERROR] robocopy not found in PATH & exit /b 1)

echo [10%%] Rebuilding jellyfin-web production bundle...
pushd "%ROOT%jellyfin-web" >nul
call npm run build:production
if errorlevel 1 (
  popd >nul
  echo [ERROR] jellyfin-web build failed.
  exit /b 1
)
if not exist "%ROOT%jellyfin-web\dist\index.html" (
  popd >nul
  echo [ERROR] jellyfin-web\dist\index.html not found after build.
  exit /b 1
)
popd >nul

echo [25%%] Preparing build context...
if exist "%ROOT%.run\pi-build-context" rmdir /s /q "%ROOT%.run\pi-build-context"
mkdir "%ROOT%.run\pi-build-context" >nul 2>nul

copy /y "%ROOT%Dockerfile.pi" "%ROOT%.run\pi-build-context\Dockerfile.pi" >nul || (echo [ERROR] Failed to copy Dockerfile.pi & exit /b 1)
copy /y "%ROOT%.dockerignore" "%ROOT%.run\pi-build-context\.dockerignore" >nul || (echo [ERROR] Failed to copy .dockerignore & exit /b 1)

echo [35%%] Copying jellyfin source...
robocopy "%ROOT%jellyfin" "%ROOT%.run\pi-build-context\jellyfin" /E /XD bin obj .run .tmp-run /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo [ERROR] robocopy jellyfin failed with code !errorlevel!.
  exit /b 1
)

echo [45%%] Copying jellyfin-web source...
robocopy "%ROOT%jellyfin-web" "%ROOT%.run\pi-build-context\jellyfin-web" /E /XD node_modules bin obj /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo [ERROR] robocopy jellyfin-web failed with code !errorlevel!.
  exit /b 1
)

echo [55%%] Creating jellyfin-pi-build.tar.gz...
if exist "%ROOT%jellyfin-pi-build.tar.gz" del /q "%ROOT%jellyfin-pi-build.tar.gz"
tar -czf "%ROOT%jellyfin-pi-build.tar.gz" -C "%ROOT%.run\pi-build-context" Dockerfile.pi .dockerignore jellyfin jellyfin-web
if errorlevel 1 (
  echo [ERROR] Failed to create jellyfin-pi-build.tar.gz.
  exit /b 1
)
for %%F in ("%ROOT%jellyfin-pi-build.tar.gz") do echo         Tar size: %%~zF bytes

echo [65%%] Uploading tar to Pi: %PI_USER%@%PI_HOST%:%PI_TAR_REMOTE%
if exist "%PSCP%" (
  "%PSCP%" -pw "%PI_PASS%" "%ROOT%jellyfin-pi-build.tar.gz" %PI_USER%@%PI_HOST%:%PI_TAR_REMOTE%
  if errorlevel 1 (
    echo [ERROR] Upload failed via pscp.
    exit /b 1
  )
) else (
  echo [WARN] pscp.exe not found at "%PSCP%".
  echo        Falling back to scp ^(you may be prompted for password^).
  scp "%ROOT%jellyfin-pi-build.tar.gz" %PI_USER%@%PI_HOST%:%PI_TAR_REMOTE%
  if errorlevel 1 (
    echo [ERROR] Upload failed via scp.
    exit /b 1
  )
)

set "TMP_SH=%TEMP%\deploy_knightflix_%RANDOM%.sh"
(
  echo set -euo pipefail
  echo echo "[70%%] Verifying uploaded tar and mounts..."
  echo ls -lh %PI_TAR_REMOTE%
  echo docker inspect %PI_CONTAINER% --format '{{range .Mounts}}{{println .Source "=>" .Destination}}{{end}}'
  echo echo "[75%%] Building Docker image on Pi..."
  echo IMAGE_TAG="knightflix:$(date +%%Y-%%m-%%d-%%H%%M)"
  echo echo "IMAGE_TAG=$IMAGE_TAG"
  echo WORKDIR="/root/knightflix-build-${IMAGE_TAG//[:\/]/_}"
  echo rm -rf "$WORKDIR"
  echo mkdir -p "$WORKDIR"
  echo tar -xzf %PI_TAR_REMOTE% -C "$WORKDIR"
  echo ls -lh "$WORKDIR/jellyfin-web/dist/index.html"
  echo DOCKER_BUILDKIT=0 docker build -t "$IMAGE_TAG" -f "$WORKDIR/Dockerfile.pi" "$WORKDIR"
  echo echo "[88%%] Recreating %PI_CONTAINER% container safely..."
  echo cfg="/srv/dockerdata/knightflix/config"
  echo cache="/var/cache/knightflix"
  echo m1="/srv/dev-disk-by-uuid-7b2260f5-9928-4ef0-a7db-5802e2b023c7"
  echo m2="/srv/dev-disk-by-uuid-4de857dc-2d58-4ecd-a473-02e1c265c87f/MediaServer"
  echo tz="Asia/Kolkata"
  echo mediaVol="$(docker inspect %PI_CONTAINER% --format '{{range .Mounts}}{{if eq .Destination "/media"}}{{.Name}}{{end}}{{end}}' 2^>/dev/null ^|^| true)"
  echo docker rm -f %PI_CONTAINER% 2^>/dev/null ^|^| true
  echo if [ -n "$mediaVol" ]; then
  echo ^  docker run -d --name %PI_CONTAINER% --restart unless-stopped \
  echo ^    -p 8097:8096 -p 8921:8920 \
  echo ^    -e "TZ=$tz" \
  echo ^    -v "$cfg:/config" -v "$cache:/cache" -v "$mediaVol:/media" -v "$m1:/media1" -v "$m2:/media2" \
  echo ^    --entrypoint dotnet \
  echo ^    "$IMAGE_TAG" /opt/jellyfin/jellyfin.dll --datadir /config --cachedir /cache
  echo else
  echo ^  docker run -d --name %PI_CONTAINER% --restart unless-stopped \
  echo ^    -p 8097:8096 -p 8921:8920 \
  echo ^    -e "TZ=$tz" \
  echo ^    -v "$cfg:/config" -v "$cache:/cache" -v "$m1:/media1" -v "$m2:/media2" \
  echo ^    --entrypoint dotnet \
  echo ^    "$IMAGE_TAG" /opt/jellyfin/jellyfin.dll --datadir /config --cachedir /cache
  echo fi
  echo echo "[95%%] Verifying deployment health..."
  echo docker ps --filter name=%PI_CONTAINER% --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}'
  echo i=0
  echo until curl -fsS http://127.0.0.1:8097/System/Info/Public ^>/dev/null; do
  echo ^  i=$((i+1))
  echo ^  echo "not ready yet ($i)"
  echo ^  sleep 3
  echo ^  [ $i -ge 40 ] ^&^& echo "still not ready, check logs" ^&^& break
  echo done
  echo curl -fsS http://127.0.0.1:8097/System/Info/Public ^| head -c 200; echo
  echo echo "[97%%] Cleaning up remote artifacts..."
  echo rm -f %PI_TAR_REMOTE%
  echo rm -rf /root/knightflix-build-*
  echo docker builder prune -af
  echo docker image prune -af
  echo echo "[100%%] Deploy complete. Open: http://%PI_HOST%:8097/web/"
) > "%TMP_SH%"

echo [70%%] Running remote build/deploy on Pi ^(realtime logs below^)...
type "%TMP_SH%" | "%PLINK%" -ssh -pw "%PI_PASS%" -batch %PI_USER%@%PI_HOST% "bash -s --"
set "REMOTE_RC=%ERRORLEVEL%"
del /q "%TMP_SH%" >nul 2>nul

if not "%REMOTE_RC%"=="0" (
  echo [ERROR] Remote deploy failed with code %REMOTE_RC%.
  exit /b %REMOTE_RC%
)

echo [100%%] All done. Cleaning up local artifacts...
if exist "%ROOT%.run\pi-build-context" rmdir /s /q "%ROOT%.run\pi-build-context"
if exist "%ROOT%jellyfin-pi-build.tar.gz" del /q "%ROOT%jellyfin-pi-build.tar.gz"

popd >nul
exit /b 0
