# Evidencias QA - Sector 09

Esta carpeta está preparada para la entrega exigida. Los resultados deben provenir de ejecuciones reales del proyecto.

1. `01_Unitarias`: salida de pruebas unitarias.
2. `02_Humo`: salida filtrada con `Category=Smoke`.
3. `03_Integracion`: salida de las pruebas de integración.
4. `04_Aceptacion_BDD`: salida de Acceptance y del proyecto Reqnroll `DoomFPS.BDD`.
5. `05_Rendimiento`: reporte generado por NBomber.
6. `06_Exploratorias`: bitácora SBTM diligenciada.
7. `07_Cobertura`: archivos de Coverlet (`coverage.cobertura.xml`) y capturas/reporte.

## Comandos
```bash
dotnet test DoomFPS.Tests/DoomFPS.Tests.csproj -c Release
dotnet test DoomFPS.Tests/DoomFPS.Tests.csproj -c Release --filter "Category=Smoke"
dotnet test DoomFPS.Tests/DoomFPS.Tests.csproj -c Release --collect:"XPlat Code Coverage"
dotnet test DoomFPS.BDD/DoomFPS.BDD.csproj -c Release
dotnet run --project DoomFPS.Performance/DoomFPS.Performance.csproj -c Release
```

Criterios del documento de entrega: suites reproducibles, cobertura objetivo >80% en Domain/Application, humo, integración, aceptación/BDD, rendimiento y exploratorias. No se incluyen métricas falsas: deben copiarse aquí después de ejecutar los comandos.
