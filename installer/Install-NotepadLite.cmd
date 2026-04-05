@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-NotepadLite.ps1"
exit /b %ERRORLEVEL%