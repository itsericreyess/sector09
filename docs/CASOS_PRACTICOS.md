# Casos prácticos: pruebas de humo y aceptación de Sector 09

Esta práctica está basada en el código del juego entregado, con sus servicios y repositorios reales. No es una lista de ejemplos ajenos al proyecto. Los identificadores HUM y ACE coinciden con el comienzo de los nombres de las pruebas C#.

## Qué demuestra cada grupo

**Humo:** una comprobación rápida de funciones críticas para decidir si se puede continuar probando: cargar, iniciar, moverse, disparar, interactuar, pausar y reintentar. No demuestra por sí sola que todo el juego esté correcto.

**Aceptación:** comprueba criterios observables que esperamos como jugadores: cuándo avanza un objetivo, cuándo se bloquea la salida, cuándo se gana y cuándo se pierde. Cada caso se organiza como Dado, Cuando y Entonces. La aprobación automatizada de estos criterios no sustituye la revisión final del profesor o usuario.

Las pruebas unitarias e integración existentes se mantienen como regresión. Humo y aceptación expresan propósitos distintos de prueba; técnicamente nuestros casos automatizados conectan varias capas reales y también son pruebas de integración. No usamos Moq en estos dos grupos nuevos; Moq continúa en las unitarias de servicios existentes.

## Cómo realizar la práctica

1. Extrae el ZIP completo y abre su carpeta en VS Code.
2. Para usar el menú de la práctica, abre `Casos_practicos.bat`.
3. Ejecuta primero `Ejecutar_pruebas_humo.bat`. Revisa que se descubran 7 casos y que ninguno falle.
4. Si pasa humo, ejecuta `Ejecutar_pruebas_aceptacion.bat`. Deben descubrirse 25 casos.
5. Toma una captura de cada ejecución donde se vea el comando, el total, las aprobadas y las fallidas.
6. Para la demostración visual, ejecuta `Jugar.bat`, pulsa F3, elige una misión con 1–7 o flechas y pulsa Enter. La práctica empieza en Recluta con equipo, pero no es modo invencible.
7. Usa M para localizar los objetivos cian numerados. Acércate, mira el dispositivo y pulsa E. Los controles cambian al dron, interruptor, válvula o carga según la misión.
8. Si un caso falla, conserva la salida y el TRX y compara lo esperado con lo observado. No lo marques aprobado sin verificarlo.

Requisitos: Windows con .NET Desktop Runtime 8 o posterior para la versión compilada. Para ejecutar las pruebas o modificar el código se necesita el SDK de .NET 8 o posterior; la primera restauración de NuGet puede necesitar Internet.

## Comandos para la terminal

```powershell
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "Category=Smoke"
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "Category=Acceptance"
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj
```

Para mostrar solamente un caso de aceptación:

```powershell
dotnet test .\DoomFPS.Tests\DoomFPS.Tests.csproj --filter "FullyQualifiedName~ACE02" --logger "console;verbosity=detailed"
```

Para generar evidencia TRX, los archivos BAT ya agregan `--logger "trx;LogFileName=humo.trx"` o `aceptacion.trx`, y `--results-directory "Evidencias"`. Un fallo de restauración o compilación no equivale a un caso de prueba fallido: primero debe poder ejecutarse la suite. Si aparecen cero pruebas descubiertas, esa ejecución no cuenta como una validación.

## Preparación y alcance de los escenarios

`DoomFPS.Tests/Scenarios/MissionScenario.cs` prepara un mapa pequeño y coloca al jugador en las condiciones iniciales de cada caso. Después la acción se envía a `GameService.Step` mediante `GameInput`. Se usan las entidades y los repositorios reales.

En algunos escenarios se prepara la posición junto al objetivo para aislar su regla: no se afirma que eso sea recorrer todo el mapa. ACE05 sí sigue al dron con movimiento real en un mapa controlado. Las pruebas de tiempo emplean cuentas de 2 segundos para ejecutarse rápido; el juego real mantiene 240 segundos en el laboratorio, 60 en la evacuación y 18 segundos de transmisión.

Los casos de humo HUM01, HUM02 y HUM07 cargan datos reales de campaña. Las pruebas anteriores también verifican la accesibilidad de los siete mapas y el combate del nivel original. La suite no constituye una prueba de equilibrio realizada por jugadores humanos.

## Qué cambia en cada misión

| Misión | Nivel | Mecánica | Criterio de avance |
|---|---|---|---|
| 1 | Hangar de acoplamiento | Activar dos relés con E, en cualquier orden | Ambos activos y alcanzar la salida |
| 2 | Sala de energía | Cruzar suelo que alterna peligro y seguridad; interruptores 1 → 2 → 3 | Los tres interruptores y alcanzar la salida |
| 3 | Bodega de carga | Escoltar un dron: exige cercanía y ruta sin enemigos junto a él | Dron en destino y jugador en la salida |
| 4 | Laboratorio | Cerrar tres válvulas antes de cuatro minutos | Sellar la fuga y evacuar |
| 5 | Arsenal | Transmitir durante 18 s dentro del círculo sin enemigos cercanos | Transmisión completa y evacuar |
| 6 | Reactor inestable | Colocar tres cargas; la última inicia 60 s de evacuación | Llegar antes de la explosión |
| 7 | Ciberdemonio | Desactivar tres pilares que lo hacen invulnerable y combatir al jefe | Pilares apagados, jefe muerto y alcanzar la salida |

## Casos de humo

| ID | Caso | Dado | Cuando | Entonces |
|---|---|---|---|---|
| HUM01 | Inicio de campaña | Una campaña nueva con servicios y repositorios reales | Iniciar en dificultad normal | Estado Playing, jugador vivo y misión de relés disponible |
| HUM02 | Carga de mapas | La fábrica de los siete niveles | Crear cada nivel | Cada mapa tiene inicio transitable y objetivos definidos |
| HUM03 | Movimiento y disparo | Un jugador con munición en una zona libre | Enviar movimiento y disparo | Cambia de posición y gasta un cartucho |
| HUM04 | Interacción | Jugador cerca de un relé y mirando el panel | Enviar Interact | El objetivo avanza una vez y genera un mensaje |
| HUM05 | Pausa | Una misión activa | Pausar, enviar acciones y reanudar | El reloj se congela en pausa y continúa al reanudar |
| HUM06 | Reintento | Una partida con daño recibido | Reintentar la misión | Se crea una partida nueva con salud y objetivos iniciales |
| HUM07 | Modo práctica | Selector de misiones disponible | Cargar cada una de las siete misiones | La misión seleccionada inicia con equipo y reglas activas |

## Casos de aceptación

| ID | Caso | Dado | Cuando | Entonces |
|---|---|---|---|---|
| ACE01 | Salida por relés | Relés sin activar y un enemigo vivo | Probar la salida, activar ambos relés y volver | Primero se bloquea; después se gana aunque quede un enemigo |
| ACE02 | Orden de interruptores | Misión eléctrica sin progreso | Activar primero el 3 y luego 1, 2, 3 | El intento incorrecto no avanza; el orden correcto apaga la electricidad |
| ACE03 | Suelo eléctrico | Jugador sobre una celda eléctrica | Pisar durante fase activa y después durante fase segura | En rojo pierde salud; en azul no recibe daño eléctrico |
| ACE04 | Dron espera | Dron activado | Alejarse y volver a acercarse | Se detiene sin acompañante y vuelve a avanzar al regresar |
| ACE05 | Escolta completa | Dron activado en una ruta despejada | Seguirlo con movimiento hasta el destino y entrar en la salida | El dron llega y la misión se gana |
| ACE06 | Fuga controlada | Cuenta atrás activa | Cerrar las tres válvulas antes del límite | El reloj de amenaza se detiene y se puede evacuar |
| ACE07 | Fuga sin controlar | Cuenta atrás activa | Dejar que llegue a cero sin cerrar las válvulas | La partida pasa a Lost: esa derrota es el resultado esperado |
| ACE08 | Transmisión interrumpible | Antena activada con progreso parcial | Salir de la zona y regresar | Fuera no avanza; conserva progreso y al completar permite salir |
| ACE09 | Zona disputada | Antena activada y enemigo a menos de 2,6 casillas | Esperar dentro del área | La transmisión permanece detenida mientras haya intrusos |
| ACE10 | Inicio de evacuación | Solo una carga colocada | Esperar, colocar las restantes y salir | No hay cuenta atrás antes de la última; escapar a tiempo da victoria |
| ACE11 | Explosión | Tres cargas colocadas | Dejar vencer la cuenta atrás | La partida termina en derrota |
| ACE12 | Escudo del jefe | Jefe vivo y tres pilares activos | Disparar, desactivar pilares y volver a disparar | Antes no pierde vida; después es vulnerable y derrotarlo habilita la salida |
| ACE13 | Pausa de misión | Una misión con cuenta atrás | Pausar e intentar activar un objetivo | Ni el tiempo ni los objetivos avanzan hasta reanudar |
| ACE14 | Sin duplicación | Un relé ya activado | Interactuar otra vez | El contador no aumenta otra vez |
| ACE15 | Distancia de interacción | Jugador lejos del panel | Mirarlo y pulsar interacción | No activa el objetivo |
| ACE16 | Mochila persistente | Mochila recogida y cartuchos por encima del límite normal | Capturar y aplicar el equipo a otro jugador | La mochila, su capacidad de 198 cartuchos y la munición se conservan |
| ACE17 | Reinicio de objetivos | Laboratorio con una válvula cerrada y tiempo transcurrido | Reintentar la misión | Se mantiene el nivel, se reinician sus objetivos y vuelve la cuenta a 240 s |

## Resultado de la revisión automatizada

Se ejecutaron 104 pruebas: 7 de humo, 25 de aceptación y 72 de regresión. Todas pasaron. La evidencia textual real está en `RESULTADOS_REVISION.txt`. Se usaron Roslyn, xUnit y Moq reales con un ejecutor local por reflexión; no se presenta como salida de VSTest ni como capturas de tu equipo.

## Evidencia manual que debes registrar

| Evidencia | Qué debe verse | Estado inicial |
|---|---|---|
| Captura 1 | Comando de humo y resultado de las 7 pruebas | Pendiente en tu equipo |
| Captura 2 | Comando de aceptación y resultado de las 25 pruebas | Pendiente en tu equipo |
| Captura 3 | F3 y selección de una misión | Pendiente en Windows |
| Captura 4 | Panel del nivel eléctrico rechazando el orden incorrecto | Pendiente en Windows |
| Captura 5 | Cambio de progreso al completar el objetivo correcto | Pendiente en Windows |
| Captura 6 | Pausa con la cuenta atrás detenida | Pendiente en Windows |

En cada evidencia escribe fecha, equipo, caso, resultado esperado, resultado observado y aprobado/fallido. No se adjuntan pantallazos inventados. El archivo de resultados de revisión sirve como respaldo de esta revisión, pero tus capturas deben proceder de tu ejecución.

## Para explicar mientras expones

“Primero ejecutamos las pruebas de humo para confirmar que las funciones principales están disponibles. Por ejemplo, comprobamos que el juego pueda crear la campaña, que el jugador se mueva y dispare, y que una interacción cambie el objetivo. Si algo básico falla, lo revisamos antes de continuar.

Después ejecutamos las pruebas de aceptación. Estas se basan en lo que esperamos que ocurra como jugadores. En el nivel eléctrico, por ejemplo, primero intentamos activar el interruptor equivocado y comprobamos que no avance. Luego activamos uno, dos y tres, y verificamos que se apague la corriente y se habilite la salida.

Usamos Dado para preparar la situación, Cuando para realizar la acción y Entonces para comprobar el resultado con Assert. Un caso que espera que el jugador pierda puede pasar correctamente: lo que se evalúa es que ocurra la derrota prevista. Por eso ACE07 pasa cuando la fuga se queda sin tiempo y la partida termina en Lost.

Las pruebas conectan Application, Domain e Infrastructure. Presentation muestra el juego, pero estas pruebas pueden revisar las reglas sin abrir la ventana. Por eso complementamos las pruebas automáticas con una revisión manual del teclado, el ratón, el audio y los indicadores de pantalla.”

## Aceptación adicional: nave y mira vertical

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
