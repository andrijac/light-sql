@echo off
rem ---------------------------------------------------------------------------
rem  run.cmd - builds LightSQL (Debug) and starts it.
rem
rem  Usage:   run.cmd [file.sql ...]
rem  Any .sql files given are opened in query tabs, e.g.  run.cmd scripts\report.sql
rem
rem  Works from any folder: paths are relative to this script, while file
rem  arguments are relative to the folder you run it from.
rem ---------------------------------------------------------------------------
setlocal

set "PROJECT=%~dp0src\LightSQL\LightSQL\LightSQL.csproj"
set "EXE=%~dp0src\LightSQL\LightSQL\bin\Debug\net10.0-windows\LightSQL.exe"

rem The .NET SDK is needed to build.
where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found. Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

rem A running LightSQL locks its .exe, so the build could not replace it.
tasklist /fi "imagename eq LightSQL.exe" 2>nul | find /i "LightSQL.exe" >nul
if not errorlevel 1 (
    echo LightSQL is already running. Close it first so it can be rebuilt.
    exit /b 1
)

echo Building LightSQL...
dotnet build "%PROJECT%" --configuration Debug --nologo --verbosity quiet
if errorlevel 1 (
    echo Build failed - see the errors above.
    exit /b 1
)

rem "start" launches the window without keeping this console waiting for it.
start "" "%EXE%" %*
echo LightSQL started.
exit /b 0
