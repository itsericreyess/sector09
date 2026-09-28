@echo off
setlocal
cd /d "%~dp0"
:menu
cls
echo DOOMFPS - CASOS PRACTICOS
echo.
echo 1. Ejecutar pruebas de humo
echo 2. Ejecutar pruebas de aceptacion
echo 3. Abrir guia de casos y evidencias
echo 4. Jugar (F3 permite elegir una mision)
echo 5. Salir
echo.
choice /c 12345 /n /m "Selecciona una opcion: "
if errorlevel 5 exit /b 0
if errorlevel 4 goto jugar
if errorlevel 3 goto guia
if errorlevel 2 goto aceptacion
call Ejecutar_pruebas_humo.bat
goto menu
:aceptacion
call Ejecutar_pruebas_aceptacion.bat
goto menu
:guia
start "" "docs\CASOS_PRACTICOS.html"
goto menu
:jugar
call Jugar.bat
goto menu
