@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo No se encontro .NET. Instala .NET Desktop Runtime 8 o posterior para Windows.
  echo Tambien sirve un SDK de .NET para Windows que incluya ese runtime.
  pause
  exit /b 1
)
if not exist "%~dp0Juego_Listo\DoomFPS.Presentation.dll" (
  echo Todavia no hay una version compilada en la carpeta Juego_Listo.
  echo Ejecuta una vez Compilar_y_jugar.bat: compila, deja la version lista
  echo y arranca el juego. Despues Jugar.bat ya funciona por si solo.
  echo.
  echo Para compilar hace falta el SDK de .NET 8 o posterior.
  pause
  exit /b 1
)
dotnet "%~dp0Juego_Listo\DoomFPS.Presentation.dll"
if errorlevel 1 (
  echo.
  echo No se pudo iniciar el juego. Revisa el mensaje anterior.
  echo Se necesita Windows y Microsoft.WindowsDesktop.App 8 o posterior.
  pause
  exit /b 1
)
