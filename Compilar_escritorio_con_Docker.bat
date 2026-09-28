@echo off
setlocal
cd /d "%~dp0"
echo SECTOR 09 - Compilar la version de escritorio con Docker
echo (no necesitas el SDK de .NET, solo Docker Desktop)
echo.
where docker >nul 2>nul
if errorlevel 1 (
  echo No se encontro Docker en este equipo.
  echo Instala Docker Desktop: https://www.docker.com/products/docker-desktop/
  echo O instala el SDK de .NET 8 y usa Compilar_y_jugar.bat
  pause
  exit /b 1
)
docker info >nul 2>nul
if errorlevel 1 (
  echo Docker esta instalado pero no esta encendido. Abre Docker Desktop,
  echo espera a que diga "Engine running" y vuelve a ejecutar este archivo.
  pause
  exit /b 1
)
docker build --target escritorio --output type=local,dest=Juego_Listo .
if errorlevel 1 (
  echo.
  echo Fallo la compilacion. Revisa el mensaje anterior.
  pause
  exit /b 1
)
echo.
echo Version de escritorio lista en Juego_Listo. Ahora abre Jugar.bat
echo (requiere .NET Desktop Runtime 8 o posterior).
pause
