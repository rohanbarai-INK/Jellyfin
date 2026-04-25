@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS1=%SCRIPT_DIR%deploy-knightflix-fixed.ps1"

if not exist "%PS1%" (
    echo Could not find deploy-knightflix-fixed.ps1 in:
    echo %SCRIPT_DIR%
    echo.
    pause
    exit /b 1
)

where pwsh >nul 2>&1
if %ERRORLEVEL%==0 (
    pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
) else (
    powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%PS1%" %*
)

set "EXITCODE=%ERRORLEVEL%"
echo.

if not "%EXITCODE%"=="0" (
    echo Deployment failed with exit code %EXITCODE%.
) else (
    echo Deployment finished successfully.
)

pause
exit /b %EXITCODE%
