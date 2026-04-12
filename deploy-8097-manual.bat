@echo off
if defined TRACE echo on
setlocal EnableExtensions EnableDelayedExpansion

REM deploy-8097-manual.bat
REM Manual (non-dev-jellyfin.bat) build + deploy + run Jellyfin on port 8097.
REM
REM What it does:
REM 1) Stops anything listening on 8097
REM 2) dotnet publish -> _deploy\server-dev-8097
REM 3) Copies jellyfin-web\dist -> _deploy\server-dev-8097\jellyfin-web\dist
REM 4) Starts Jellyfin with --datadir/--cachedir/--configdir/--logdir/--webdir/--ffmpeg
REM 5) Writes PID + stdout/stderr logs under .run\jf-8097
REM
REM Usage:
REM   deploy-8097-manual.bat
REM
REM Optional env vars:
REM   CONFIG=Debug|Release      (default Debug)
REM   SKIP_WEB_COPY=1          Skip robocopy of jellyfin-web\dist
REM   SKIP_STOP=1              Skip stopping existing process on 8097
REM   JELLYFIN_FFMPEG=...      Explicit ffmpeg.exe path (optional)

set "PORT=8097"
if "%CONFIG%"=="" set "CONFIG=Debug"

set "ROOT=%~dp0"
pushd "%ROOT%" >nul

set "RUN_DIR=%ROOT%.run\jf-%PORT%"
set "DEPLOY_DIR=%ROOT%_deploy\server-dev-%PORT%"
set "WEB_REPO_DIST=%ROOT%jellyfin-web\dist"
set "WEB_DEPLOY_DIST=%DEPLOY_DIR%\jellyfin-web\dist"
set "SERVER_DLL=%DEPLOY_DIR%\jellyfin.dll"
set "OUT_LOG=%RUN_DIR%\start_out_manual_%PORT%.txt"
set "ERR_LOG=%RUN_DIR%\start_err_manual_%PORT%.txt"
set "PID_FILE=%RUN_DIR%\server_manual_%PORT%.pid"

if not exist "%RUN_DIR%" mkdir "%RUN_DIR%" >nul 2>nul
if not exist "%RUN_DIR%\data" mkdir "%RUN_DIR%\data" >nul 2>nul
if not exist "%RUN_DIR%\cache" mkdir "%RUN_DIR%\cache" >nul 2>nul
if not exist "%RUN_DIR%\config" mkdir "%RUN_DIR%\config" >nul 2>nul
if not exist "%RUN_DIR%\logs" mkdir "%RUN_DIR%\logs" >nul 2>nul

echo === Manual Deploy to port %PORT% (CONFIG=%CONFIG%) ===

REM Stop anything currently listening on the port.
if defined SKIP_STOP (
  echo SKIP_STOP=1 set, not stopping existing process on %PORT%.
) else (
  echo Stopping any process on port %PORT% ...
  for /f "tokens=5" %%P in ('netstat -ano ^| findstr /r /c:":%PORT% .*LISTENING"') do (
    echo   taskkill /PID %%P /F
    taskkill /PID %%P /F >nul 2>nul
  )
)

REM Publish server to deploy dir.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo dotnet not found in PATH. Install .NET SDK then re-run.
  exit /b 1
)

echo === dotnet publish jellyfin server ===
dotnet publish jellyfin\Jellyfin.Server\Jellyfin.Server.csproj -c %CONFIG% -o "%DEPLOY_DIR%" -m:1
if errorlevel 1 (
  echo dotnet publish failed.
  exit /b 1
)

REM Copy web dist into deploy dir (unless skipped).
if defined SKIP_WEB_COPY (
  echo SKIP_WEB_COPY=1 set, not copying jellyfin-web\dist.
  goto :start
)

if not exist "%WEB_REPO_DIST%" (
  echo Web dist not found: "%WEB_REPO_DIST%"
  echo Build jellyfin-web first, or re-run with SKIP_WEB_COPY=1.
  exit /b 1
)

echo === Copy jellyfin-web dist into deploy folder ===
if not exist "%DEPLOY_DIR%" (
  echo Deploy folder missing: "%DEPLOY_DIR%"
  exit /b 1
)

REM robocopy returns non-zero on success; treat 0-7 as OK, 8+ as failure.
robocopy "%WEB_REPO_DIST%" "%WEB_DEPLOY_DIST%" /MIR /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 (
  echo robocopy failed with code %ERRORLEVEL%.
  exit /b 1
)

:start
if not exist "%SERVER_DLL%" (
  echo Server not found after publish: "%SERVER_DLL%"
  exit /b 1
)

REM Resolve ffmpeg path.
set "FFMPEG=%JELLYFIN_FFMPEG%"
if "%FFMPEG%"=="" if exist "%ProgramFiles%\Jellyfin\Server\ffmpeg.exe" set "FFMPEG=%ProgramFiles%\Jellyfin\Server\ffmpeg.exe"
REM Avoid parenthesized blocks here: %ProgramFiles(x86)% expands to a value with parentheses in it.
if "%FFMPEG%"=="" if exist "%ProgramFiles(x86)%\Jellyfin\Server\ffmpeg.exe" set "FFMPEG=%ProgramFiles(x86)%\Jellyfin\Server\ffmpeg.exe"
if "%FFMPEG%"=="" goto :ffmpeg_missing

REM Clean logs/pid.
if exist "%OUT_LOG%" del /q "%OUT_LOG%" >nul 2>nul
if exist "%ERR_LOG%" del /q "%ERR_LOG%" >nul 2>nul
if exist "%PID_FILE%" del /q "%PID_FILE%" >nul 2>nul

echo === Starting Jellyfin (background) ===
echo   URL: http://127.0.0.1:%PORT%/web/index.html
echo   Logs: "%OUT_LOG%" and "%ERR_LOG%"

REM Use PowerShell only as a launcher (Start-Process + redirect + PID file).
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$serverDll='%SERVER_DLL%'; $runDir='%RUN_DIR%'; $webDir='%WEB_DEPLOY_DIST%'; $ff='%FFMPEG%';" ^
  "$out='%OUT_LOG%'; $err='%ERR_LOG%'; $pidFile='%PID_FILE%';" ^
  "$args=('\"{0}\" --datadir \"{1}\\data\" --cachedir \"{1}\\cache\" --configdir \"{1}\\config\" --logdir \"{1}\\logs\" --webdir \"{2}\" --ffmpeg \"{3}\"' -f $serverDll,$runDir,$webDir,$ff);" ^
  "$p=Start-Process -FilePath 'dotnet' -ArgumentList $args -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -WindowStyle Hidden;" ^
  "Set-Content -LiteralPath $pidFile -Value $p.Id;"

exit /b %ERRORLEVEL%

:ffmpeg_missing
echo ffmpeg.exe not found. Set JELLYFIN_FFMPEG to a full path, or install Jellyfin Server (ffmpeg).
exit /b 1
