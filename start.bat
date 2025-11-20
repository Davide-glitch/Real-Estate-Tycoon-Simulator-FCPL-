@echo off
setlocal EnableExtensions

echo ====================================
echo EstateArchitect - Start or Restart
echo ====================================

REM Config
set PORT=7227
set URL=https://localhost:%PORT%

echo Checking if something is listening on port %PORT% ...
set PID=
for /f "tokens=5" %%a in ('netstat -ano ^| findstr /r ":%PORT% .*LISTENING"') do set PID=%%a

if defined PID (
    echo Found PID %PID% listening on %PORT%. Stopping it...
    taskkill /F /PID %PID% >nul 2>&1
    timeout /t 1 >nul
) else (
    echo No process found on port %PORT%.
)

REM Also try stopping any lingering apphost that may lock the exe
taskkill /F /IM DavidEstateArchitect.exe /T >nul 2>&1
timeout /t 1 >nul

echo Building project...
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo Build failed. Exiting.
    exit /b %ERRORLEVEL%
)

echo Launching app on %URL% ...
start "EstateArchitect" cmd /c "dotnet run --launch-profile https"
start "" "%URL%"

endlocal
