@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM stop-jellyfin.bat
REM Stops the locally running Jellyfin dev server started by dev-jellyfin.bat.
REM
REM Usage:
REM   stop-jellyfin.bat
REM   stop-jellyfin.bat [port]
REM
REM Default:
REM   port = 8097

set "PORT=%~1"
if "%PORT%"=="" set "PORT=8097"

set "ROOT=%~dp0"
pushd "%ROOT%" >nul

set "RUN_DIR=%ROOT%.run\jf-%PORT%"
set "PID_FILE=%RUN_DIR%\server_dev_%PORT%.pid"

echo === Stop Jellyfin on port %PORT% ===

REM Prefer the PID file from dev-jellyfin.bat if it exists.
if exist "%PID_FILE%" (
  set "PID="
  for /f "usebackq delims=" %%P in (`type "%PID_FILE%" 2^>nul`) do set "PID=%%P"
  if not "!PID!"=="" (
    echo Stopping PID !PID! from "%PID_FILE%"...
    taskkill /PID !PID! /F >nul 2>nul
  )
  del /q "%PID_FILE%" >nul 2>nul
)

REM Also kill anything still LISTENING on the port.
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /r /c:":%PORT% .*LISTENING"') do (
  echo Stopping PID %%P listening on %PORT%...
  taskkill /PID %%P /F >nul 2>nul
)

echo Stopped.
exit /b 0
