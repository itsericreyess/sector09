# Entrega QA - Sector 09

## Implementación incluida
- Pruebas unitarias, integración, aceptación y humo existentes en `DoomFPS.Tests`.
- Coverlet Collector agregado para cobertura.
- BDD con Reqnroll y Gherkin en `DoomFPS.BDD`.
- Prueba de carga con NBomber en `DoomFPS.Performance` (50 operaciones/segundo durante 30 s).
- Pipeline GitHub Actions en `.github/workflows/qa.yml`.
- Bitácora SBTM para pruebas exploratorias.
- Estructura de carpetas para evidencias reales.
- Script `EJECUTAR_QA_COMPLETO.bat` para ejecutar la batería.

## Importante
Las métricas y veredictos finales no se inventaron. Deben generarse ejecutando el proyecto en una máquina con .NET 8 SDK y luego adjuntar las salidas en `Evidencias`. Esto permite que la entrega corresponda realmente a Sector 09.
