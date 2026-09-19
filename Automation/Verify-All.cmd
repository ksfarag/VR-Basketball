@echo off
setlocal
if defined PWSH_EXE goto configured
where pwsh.exe >nul 2>nul
if errorlevel 1 goto standard
pwsh.exe -NoProfile -File "%~dp0Verify-All.ps1" %*
exit /b %errorlevel%
:standard
if not exist "%ProgramFiles%\PowerShell\7\pwsh.exe" goto missing
"%ProgramFiles%\PowerShell\7\pwsh.exe" -NoProfile -File "%~dp0Verify-All.ps1" %*
exit /b %errorlevel%
:configured
if not exist "%PWSH_EXE%" goto missing
"%PWSH_EXE%" -NoProfile -File "%~dp0Verify-All.ps1" %*
exit /b %errorlevel%
:missing
echo PowerShell 7.2 or newer is required. Add pwsh.exe to PATH or set PWSH_EXE to its full path.
exit /b 1
