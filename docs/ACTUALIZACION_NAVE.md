# Sector 09 · Emergencia orbital

La nave está averiada y se dirige hacia un planeta. La campaña progresa desde el hangar hasta el puente: recuperar comunicaciones, restablecer energía, escoltar al dron, cerrar el refrigerante, transmitir una trayectoria segura, separar el reactor y recuperar los controles de vuelo.

## Cómo probarlo

Extrae todo el ZIP y abre Jugar.bat. Desde el menú, F3 permite elegir cualquier cubierta. Usa el modo local si no tienes Firebase configurado. Haz clic para capturar el ratón y muévelo arriba, abajo y hacia los lados; Esc pausa y libera el cursor. RePág y AvPág también inclinan la cámara; Inicio la centra. M abre el mapa y E activa objetivos cercanos.

Los escenarios mantienen estética retro. Se ha subido la resolución interna de 480 × 270 a 640 × 360. Los nuevos materiales y panoramas proceden del código; no requieren recursos comerciales ni descargar imágenes. Ventanales y claraboyas muestran estrellas y el planeta, que se ve más grande en las últimas cubiertas. No es una simulación astronómica: el acercamiento se representa por nivel.

La cámara permite rotación horizontal completa e inclinación vertical de ±55°. El motor continúa siendo 2.5D: inclina la proyección; el jugador permanece a la misma altura. No hay saltos, vuelo o giro sobre el propio eje longitudinal. Los disparos sí comprueban altura y colisión con techo/suelo.

## Casos nuevos, ejecutables con el filtro Acceptance

| Caso | Dado | Cuando | Entonces |
|---|---|---|---|
| NAV01 | Jugador quieto | Girar horizontalmente e inclinar la vista; después pulsar Inicio | Cambian la orientación y la inclinación, no la posición; Inicio centra la mira |
| NAV02 | Partida activa | Inclinar hasta ambos límites y luego intentar mirar durante la pausa | Se limita a ±55° y la pausa conserva la orientación |
| NAV03 | Enemigo enfrente | Disparar al techo y después al enemigo con la mira centrada | El primer disparo falla; el segundo hace daño |
| NAV04 | Fusil de plasma equipado | Disparar hacia arriba | El proyectil gana altura y desaparece al chocar con el techo |
| NAV05 | Siete niveles generados | Comparar mapas y comprobar ventanales | Siete geometrías distintas; los ventanales impiden atravesar el casco |
| NAV06 | Puente con escudo desactivado | Agotar la cuenta atrás sin llegar al timón | La misión termina en derrota por impacto |
| NAV07 | Puente despejado y objetivos completos | Llegar al timón antes de terminar el tiempo | La misión termina en victoria |
| NAV08 | Datos de campaña | Consultar los plazos del reactor y el puente | 60 s para abandonar el reactor tras la última carga; 300 s desde el inicio del puente |

## Validación

104 pruebas aprobadas: 7 de humo, 25 de aceptación y 72 de regresión. Se compilaron los cuatro proyectos con referencias oficiales de .NET y Windows Desktop. Las pruebas se ejecutaron con xUnit/Moq reales mediante reflexión. Se generaron y revisaron imágenes del renderizador de las siete cubiertas; no son capturas de la ventana de Windows.

Queda pendiente probar en Windows la captura de ratón, el sonido, los menús y el rendimiento en el equipo del jugador. Las pruebas de reglas no prueban Firebase ni garantizan el equilibrio de todos los recorridos con jugadores.

```powershell
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "Category=Acceptance" --logger "console;verbosity=normal"
```
