@echo off
setlocal
cd /d "%~dp0"
echo === SECTOR 09 - QA COMPLETO ===
dotnet restore DoomFPS.sln
dotnet test DoomFPS.Tests\DoomFPS.Tests.csproj -c Release
dotnet test DoomFPS.Tests\DoomFPS.Tests.csproj -c Release --filter "Category=Smoke"
dotnet test DoomFPS.Tests\DoomFPS.Tests.csproj -c Release --collect:"XPlat Code Coverage"
dotnet test DoomFPS.BDD\DoomFPS.BDD.csproj -c Release
dotnet run --project DoomFPS.Performance\DoomFPS.Performance.csproj -c Release
pause
