@echo off
setlocal
cd /d "%~dp0"
echo Compilando DoomFPS  ·  Sector 09 ...
echo.
dotnet publish "DoomFPS.Presentation\DoomFPS.Presentation.csproj" -c Release -o "Juego_Listo" --nologo
if errorlevel 1 (
  echo.
  echo Revisa el error anterior. Se requiere el SDK de .NET 8 o posterior.
  pause
  exit /b 1
)
echo.
echo Version lista guardada en Juego_Listo. Iniciando el juego...
dotnet "%~dp0Juego_Listo\DoomFPS.Presentation.dll"
if errorlevel 1 (
  echo.
  echo No se pudo iniciar el juego. Revisa el mensaje anterior.
  pause
  exit /b 1
)
