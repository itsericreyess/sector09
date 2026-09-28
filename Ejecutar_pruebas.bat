@echo off
setlocal
cd /d "%~dp0"
set DOTNET_ROLL_FORWARD=Major
dotnet test "DoomFPS.Tests\DoomFPS.Tests.csproj"
set "test_exit=%ERRORLEVEL%"
echo.
pause
exit /b %test_exit%
