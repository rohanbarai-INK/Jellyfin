@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM dev-jellyfin.bat
REM Build + deploy Jellyfin Server + Jellyfin Web for local Windows development (no Docker).
REM
REM Usage:
REM   dev-jellyfin.bat
REM   dev-jellyfin.bat all [port] [Debug|Release]
REM   dev-jellyfin.bat build [port] [Debug|Release]
REM   dev-jellyfin.bat run [port]
REM   dev-jellyfin.bat stop [port]
REM
REM Defaults:
REM   port = 8097
REM   configuration = Debug
REM
REM Optional env vars:
REM   SKIP_WEB=1            Skip building jellyfin-web
REM   SKIP_SERVER=1         Skip publishing server
REM   SKIP_COPY_WEB=1       Skip copying dist into deploy folder (server will use repo dist)
REM   JELLYFIN_FFMPEG=...   Path to ffmpeg.exe (optional)

set "CMD=%~1"
if /i "%CMD%"=="" set "CMD=all"

set "PORT=%~2"
if "%PORT%"=="" set "PORT=8097"

set "CONFIG=%~3"
if "%CONFIG%"=="" set "CONFIG=Debug"

REM If user passed "all" explicitly, shift args pattern differs.
if /i "%CMD%"=="all" (
  if "%~2"=="" set "PORT=8097"
  if "%~3"=="" set "CONFIG=Debug"
) else if /i "%CMD%"=="build" (
  set "PORT=%~2"
  if "%PORT%"=="" set "PORT=8097"
  set "CONFIG=%~3"
  if "%CONFIG%"=="" set "CONFIG=Debug"
) else if /i "%CMD%"=="run" (
  set "PORT=%~2"
  if "%PORT%"=="" set "PORT=8097"
) else if /i "%CMD%"=="stop" (
  set "PORT=%~2"
  if "%PORT%"=="" set "PORT=8097"
)

set "ROOT=%~dp0"
pushd "%ROOT%" >nul

set "RUN_DIR=%ROOT%.run\jf-%PORT%"
set "DEPLOY_DIR=%ROOT%_deploy\server-dev-%PORT%"
set "WEB_REPO_DIST=%ROOT%jellyfin-web\dist"
set "WEB_DEPLOY_DIST=%DEPLOY_DIR%\jellyfin-web\dist"
set "SERVER_DLL=%DEPLOY_DIR%\jellyfin.dll"
set "OUT_LOG=%RUN_DIR%\start_out_dev_current.txt"
set "ERR_LOG=%RUN_DIR%\start_err_dev_current.txt"

REM Ensure run directories exist (don't overwrite config/data).
if not exist "%RUN_DIR%" mkdir "%RUN_DIR%" >nul 2>nul
if not exist "%RUN_DIR%\data" mkdir "%RUN_DIR%\data" >nul 2>nul
if not exist "%RUN_DIR%\cache" mkdir "%RUN_DIR%\cache" >nul 2>nul
if not exist "%RUN_DIR%\config" mkdir "%RUN_DIR%\config" >nul 2>nul
if not exist "%RUN_DIR%\logs" mkdir "%RUN_DIR%\logs" >nul 2>nul

if /i "%CMD%"=="stop" goto :stop
if /i "%CMD%"=="run" goto :run
if /i "%CMD%"=="build" goto :build
if /i "%CMD%"=="all" goto :all

echo Unknown command: %CMD%
echo.
echo Try:
echo   dev-jellyfin.bat all 8097 Debug
echo   dev-jellyfin.bat stop 8097
exit /b 2

:all
call :build
if errorlevel 1 exit /b 1
call :run
exit /b %ERRORLEVEL%

:build
echo === Build (%CONFIG%) port=%PORT% ===

REM Server publish
if defined SKIP_SERVER (
  echo SKIP_SERVER=1 set, skipping dotnet publish.
  goto :build_web
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo dotnet not found in PATH. Install .NET SDK then re-run.
  exit /b 1
)

set "MSBuildEnableWorkloadResolver=false"
dotnet publish jellyfin\Jellyfin.Server\Jellyfin.Server.csproj -c %CONFIG% -o "%DEPLOY_DIR%" -m:1
if errorlevel 1 (
  echo dotnet publish failed.
  exit /b 1
)

:build_web
if defined SKIP_WEB (
  echo SKIP_WEB=1 set, skipping jellyfin-web build.
  goto :copy_web
)

REM Default behavior: if we already have a built dist, use it.
REM Set FORCE_WEB_BUILD=1 to force npm install/build (requires Node 24+ and npm 11+).
if not defined FORCE_WEB_BUILD (
  if exist "%WEB_REPO_DIST%" set "SKIP_WEB=1"
)
if defined SKIP_WEB (
  echo Using existing "%WEB_REPO_DIST%" and skipping jellyfin-web build. Set FORCE_WEB_BUILD=1 to rebuild.
  goto :copy_web
)

where node >nul 2>nul
if errorlevel 1 (
  echo node not found in PATH. Install Node.js (node + npm) then re-run.
  exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
  echo npm not found in PATH. Install Node.js (includes npm) then re-run.
  exit /b 1
)

REM jellyfin-web requires node 24+ and npm 11+ (see jellyfin-web/package.json engines).
set "NODE_MAJOR="
set "NODE_TMP="
for /f "tokens=1 delims=." %%A in ('node -v') do set "NODE_TMP=%%A"
if not "%NODE_TMP%"=="" set "NODE_MAJOR=%NODE_TMP:~1%"
set "NPM_MAJOR="
for /f "tokens=1 delims=." %%A in ('npm -v') do set "NPM_MAJOR=%%A"

if "%NODE_MAJOR%"=="" goto :web_engines_bad
if "%NPM_MAJOR%"=="" goto :web_engines_bad
if %NODE_MAJOR% LSS 24 goto :web_engines_bad
if %NPM_MAJOR% LSS 11 goto :web_engines_bad

pushd jellyfin-web >nul
echo === jellyfin-web: npm install ===
if exist package-lock.json (
  npm ci
) else (
  npm install
)
if errorlevel 1 (
  popd >nul
  echo npm install failed.
  exit /b 1
)

echo === jellyfin-web: build:production ===
npm run build:production
if errorlevel 1 (
  popd >nul
  echo npm run build:production failed.
  exit /b 1
)
popd >nul

:copy_web
if defined SKIP_COPY_WEB (
  echo SKIP_COPY_WEB=1 set, skipping copying dist into deploy folder.
  goto :done_build
)

if not exist "%WEB_REPO_DIST%" (
  echo Web dist not found at "%WEB_REPO_DIST%".
  echo Build jellyfin-web first, or set SKIP_COPY_WEB=1 to run from repo dist path.
  exit /b 1
)

echo === Copy web dist into deploy folder ===
if not exist "%DEPLOY_DIR%" (
  echo Deploy folder missing: "%DEPLOY_DIR%"
  echo Run dotnet publish first, or set SKIP_SERVER=1 if you only want web build.
  exit /b 1
)

REM robocopy returns non-zero on success; treat 0-7 as OK, 8+ as failure.
robocopy "%WEB_REPO_DIST%" "%WEB_DEPLOY_DIST%" /MIR /NFL /NDL /NJH /NJS /NP >nul
set "RC=%ERRORLEVEL%"
if %RC% GEQ 8 (
  echo robocopy failed with code %RC%.
  exit /b 1
)
cmd /c exit 0

:done_build
echo Build complete.
exit /b 0

:web_engines_bad
echo Detected node v%NODE_MAJOR% and npm v%NPM_MAJOR% - jellyfin-web wants node 24+ and npm 11+.
echo Either install the newer Node/npm, or run without FORCE_WEB_BUILD and keep using the existing dist.
exit /b 1

:stop
echo === Stop Jellyfin on port %PORT% ===
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /r /c:":%PORT% .*LISTENING"') do (
  echo Stopping PID %%P ...
  taskkill /PID %%P /F >nul 2>nul
)
exit /b 0

:run
echo === Run Jellyfin on http://127.0.0.1:%PORT% ===

if not exist "%SERVER_DLL%" (
  echo Server not published: "%SERVER_DLL%"
  echo Run: dev-jellyfin.bat build %PORT% %CONFIG%
  exit /b 1
)

call :stop

set "FFMPEG=%JELLYFIN_FFMPEG%"
if "%FFMPEG%"=="" (
  if exist "%ProgramFiles%\Jellyfin\Server\ffmpeg.exe" set "FFMPEG=%ProgramFiles%\Jellyfin\Server\ffmpeg.exe"
)
if "%FFMPEG%"=="" (
  if exist "%ProgramFiles(x86)%\Jellyfin\Server\ffmpeg.exe" set "FFMPEG=%ProgramFiles(x86)%\Jellyfin\Server\ffmpeg.exe"
)

set "WEBDIR=%WEB_DEPLOY_DIST%"
if defined SKIP_COPY_WEB (
  set "WEBDIR=%WEB_REPO_DIST%"
)

if not exist "%WEBDIR%" (
  echo Webdir not found: "%WEBDIR%"
  echo Build jellyfin-web first or set SKIP_WEB=1 only if you're using an existing dist.
  exit /b 1
)

REM Launch in background; logs go to .run\jf-%PORT%.
if exist "%OUT_LOG%" del /q "%OUT_LOG%" >nul 2>nul
if exist "%ERR_LOG%" del /q "%ERR_LOG%" >nul 2>nul

set "PID_FILE=%RUN_DIR%\server_dev_%PORT%.pid"
if exist "%PID_FILE%" del /q "%PID_FILE%" >nul 2>nul

echo Starting... (logs: "%OUT_LOG%")
powershell -NoProfile -ExecutionPolicy Bypass -Command "$serverDll='%SERVER_DLL%'; $runDir='%RUN_DIR%'; $webDir='%WEBDIR%'; $ff='%FFMPEG%'; $cmd=('\"{0}\" --datadir \"{1}\\data\" --cachedir \"{1}\\cache\" --configdir \"{1}\\config\" --logdir \"{1}\\logs\" --webdir \"{2}\"' -f $serverDll,$runDir,$webDir); if($ff -ne ''){ $cmd += (' --ffmpeg \"{0}\"' -f $ff) }; $p=Start-Process -FilePath 'dotnet' -ArgumentList $cmd -RedirectStandardOutput '%OUT_LOG%' -RedirectStandardError '%ERR_LOG%' -PassThru -WindowStyle Hidden; Set-Content -LiteralPath '%PID_FILE%' -Value $p.Id;"

echo Open: http://127.0.0.1:%PORT%/web/index.html
exit /b 0
