@echo off
setlocal
cd /d "%~dp0"
echo SECTOR 09 - Version web en Docker
echo.
where docker >nul 2>nul
if errorlevel 1 (
  echo No se encontro Docker. Instala Docker Desktop y vuelve a intentarlo.
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
echo Construyendo la imagen (la primera vez tarda varios minutos)...
docker compose up -d --build
if errorlevel 1 (
  echo.
  echo Fallo la construccion. Revisa el mensaje anterior.
  pause
  exit /b 1
)
echo.
echo Listo: http://localhost:8080
start "" "http://localhost:8080"
pause
