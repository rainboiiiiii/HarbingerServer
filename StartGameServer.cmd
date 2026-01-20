@echo off
set PORT=%1
if "%PORT%"=="" set PORT=5276
set URLS=http://localhost:%PORT%
echo Starting GameBackend.Api on %URLS%
GameBackend.Api.exe --urls "%URLS%"

