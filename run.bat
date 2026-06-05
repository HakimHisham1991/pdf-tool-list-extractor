@echo off
setlocal
cd /d "%~dp0ToolingExtractor\src\ToolingExtractor.Web"
echo Starting ToolingExtractor...
echo Working directory: %CD%
echo.
dotnet run
if errorlevel 1 (
    echo.
    echo dotnet run failed with exit code %errorlevel%.
    pause
)
