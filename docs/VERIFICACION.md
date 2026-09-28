# Verificación de esta versión

Fecha: 16 de septiembre de 2026.

**104 pruebas ejecutadas: 104 aprobadas, 0 fallidas.**

| Grupo | Casos | Resultado |
|---|---:|---|
| Humo (Smoke) | 7 | Aprobados |
| Aceptación (Acceptance) | 25 | Aprobados |
| Regresión (Unit e Integration existentes) | 72 | Aprobados |

La salida completa está en RESULTADOS_REVISION.txt. CASOS_PRACTICOS.html y CASOS_PRACTICOS.md relacionan los casos nuevos con métodos de prueba y procedimientos para exponer.

## Método real

Se compiló con Roslyn del SDK oficial .NET 8.0.408. Domain, Application, Infrastructure y Presentation compilaron contra los ensamblados de referencia oficiales, incluidos los de Windows Desktop 8.0.15. No se usaron definiciones falsas de Windows Forms.

Las 104 pruebas se ejecutaron mediante un ejecutor por reflexión que invoca los métodos Fact, con las bibliotecas reales de xUnit 2.6.6 y Moq 4.20.70 y sus dependencias. Esto incluye las pruebas que usan mocks. No se presenta como una ejecución de VSTest ni se inventa un archivo TRX: los BAT generan el TRX al ejecutar dotnet test en el equipo del usuario.

Se conservaron los casos anteriores y se adaptaron dos pruebas de CampaignIntegrationTests a los objetivos nuevos: el recorrido de la primera misión activa relés y la salida final requiere desactivar pilones además de matar al jefe. Las pruebas del nivel original y sus reglas de puertas siguen incluidas.

Se comprobó la geometría y accesibilidad de los siete mapas. Se ejecutó el recorrido automatizado de la primera misión con entradas de juego. Los demás criterios nuevos se verificaron en escenarios controlados; el de escolta incluye caminar junto al dron hasta salir. Algunos relojes se acortan en las pruebas para verificar sus límites; la campaña usa 240 segundos para la fuga, 18 para transmitir y 60 para escapar.

PixelRenderer produjo imágenes sin ventana de las mecánicas de los niveles 2, 3 y 5: Mecanica_nivel_2.png, Mecanica_nivel_3.png y Mecanica_nivel_5.png. Son imágenes del renderizador, no capturas de una sesión de Windows. La compilación de Presentation se repitió después del ajuste final del HUD y terminó sin errores.

## Qué queda por comprobar manualmente

El entorno de revisión es Linux. No se abrió la ventana de Windows ni se ejecutaron sus BAT. Quedan pendientes el arranque gráfico, teclas y ratón, audio, legibilidad del HUD, selección F3 y los recorridos completos de las siete misiones con jugadores. Tampoco se afirma que su dificultad esté equilibrada por haber aprobado las pruebas de reglas.

Los ensamblados compilados se incluyen en Juego_Listo para abrir con Jugar.bat en Windows y .NET Desktop Runtime 8 o posterior. Si se modifica el código, Compilar_y_jugar.bat publica de nuevo desde los proyectos. Compilar y ejecutar pruebas requiere SDK; restaurar paquetes requiere acceso a NuGet.

## Reproducir

```powershell
dotnet build .\DoomFPS.sln
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "Category=Smoke"
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "Category=Acceptance"
```

Usa Casos_practicos.bat para la guía y las ejecuciones filtradas. Registra las observaciones manuales como pendientes, aprobadas o fallidas con evidencia real; no copies el resultado automatizado como si fuera una prueba visual ya realizada.

## Revisión espacial del 16 de septiembre

La suite actual incluye ocho casos NAV adicionales para mirar en ambos ejes, límites y pausa, daño según altura, plasma y techo, mapas distintos con ventanales sólidos, derrota por impacto, victoria en el timón y duración de las emergencias. El ayudante de preparación de interacción se adaptó para elegir un lado libre de cada objetivo: los nuevos planos pueden tener una pared a su izquierda.

Se conservaron las 96 pruebas anteriores; el recorrido automatizado de la primera misión también pasa con el nuevo hangar. Los renders actuales están en Nave_escenarios.png y Nave_nivel_1.png a Nave_nivel_7.png. Se revisaron las siete cubiertas y vistas inclinadas. Las imágenes previas de los niveles eléctricos, dron y transmisión pertenecen a la versión anterior.
