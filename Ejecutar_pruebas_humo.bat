@echo off
setlocal
cd /d "%~dp0"
set DOTNET_ROLL_FORWARD=Major
echo Pruebas de HUMO - DoomFPS
echo.
dotnet test "DoomFPS.Tests\DoomFPS.Tests.csproj" --filter "Category=Smoke" --logger "console;verbosity=normal" --logger "trx;LogFileName=humo.trx" --results-directory "Evidencias"
set "test_exit=%ERRORLEVEL%"
echo.
echo Si la ejecucion genero el informe, revisalo en Evidencias\humo.trx
pause
exit /b %test_exit%
