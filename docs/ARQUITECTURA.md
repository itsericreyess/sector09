# Arquitectura por capas

## Responsabilidades

| Capa | Qué contiene y qué hace |
|---|---|
| Domain | Entidades Player, Enemy, Weapon, Door y GameWorld. MissionPlan describe una misión; MissionState conserva nodos, progreso, escudo y relojes, y aplica reglas de secuencia y finalización. No conoce ventanas ni repositorios concretos. |
| Application | CombatService y DoorService conservan sus contratos. GameService coordina el paso de simulación, movimiento y combate; MissionService coordina interacción, daño ambiental, escolta y defensa. CampaignService construye, avanza, reintenta o inicia una práctica. Trabaja con IGameStateRepository, ICampaignRepository e ILevelSource. |
| Infrastructure | Implementa los repositorios en memoria y genera niveles. CampaignLevelFactory crea los mapas; MissionPlanFactory coloca objetivos en posiciones alcanzables. StationLevelFactory conserva el nivel original. |
| Presentation | GameForm transforma teclado y ratón en GameInput. PixelRenderer dibuja el estado: enemigos, consolas, dron y zonas. La ventana muestra menús, progreso, mapa y sonido. Program conecta las implementaciones con los servicios. |
| Tests | Unit aísla reglas y usa Moq para algunos repositorios. Integration conecta componentes reales. Smoke selecciona comprobaciones esenciales; Acceptance verifica criterios de las nuevas misiones. |

## Cómo se comunican

La presentación llama a GameService.Step con GameInput. El servicio obtiene el jugador y los enemigos mediante la interfaz del repositorio. Para un disparo calcula el impacto, respeta el escudo de misión y delega el daño en CombatService. Para una interacción consulta MissionService, que comprueba cercanía, orientación y visibilidad antes de activar un nodo de MissionState. Para una puerta tradicional usa DoorService.

Cada paso activo actualiza enemigos y misión: MissionService determina si una zona está disputada, si el dron puede moverse y si el suelo daña; MissionState registra avance y tiempo. GameService decide victoria o derrota. La presentación lee ese estado para dibujarlo; no decide si una válvula cuenta ni si el jefe es vulnerable.

No hay negociación por red: la lógica de negocio es el conjunto de reglas del juego. Las capas se comunican por llamadas a métodos, objetos e interfaces. Application referencia Domain; Infrastructure referencia Application; Presentation conecta los servicios con Infrastructure. Domain no referencia las otras capas.

## Cuenta, Firebase e historial

La pantalla de acceso pertenece a Presentation, pero solo recoge datos y llama a `IPlayerAccountService`. Infrastructure implementa esa interfaz con Firebase Authentication REST y Realtime Database. El token se conserva en memoria; cada resultado terminado se guarda bajo el UID autenticado con nivel, puntuación, bajas, segundos y fecha. F4 consulta el historial. Si no hay variables de Firebase se inyecta un servicio local para demostración, sin cambiar las reglas del juego.

## Campaña y tiempo

CampaignService usa ILevelSource para obtener el mapa y su MissionPlan. Construye un GameService y guarda un punto de control del equipo. Al avanzar captura el equipo, aplica reabastecimiento y crea la misión siguiente; al reintentar reconstruye el nivel con su objetivo y reloj iniciales. La mochila se conserva en el Loadout.

La simulación usa 60 pasos por segundo. Pausar impide avanzar la misión. La fuga empieza con el nivel; la demolición empieza al colocar la última carga. La transmisión acumula únicamente el tiempo dentro de la zona sin enemigos cerca.

El dron busca rutas por celdas mediante BFS y respeta colisiones; se detiene si pierde la escolta o hay amenazas próximas. Los enemigos mantienen sus patrones anteriores y no comparten ese buscador de rutas.

## Compatibilidad y alcance

Se mantienen las entidades, servicios e interfaces del proyecto base. Las puertas y tarjetas permanecen en el nivel original y sus pruebas; la campaña nueva usa objetivos distintos. GameService conserva el criterio antiguo de bajas cuando recibe un nivel sin MissionPlan.

La persistencia es en memoria: cerrar el juego pierde la partida. Es un FPS de raycasting para un jugador con Windows Forms, sin motor externo ni recursos comerciales de Doom. La validación automatizada de reglas no sustituye probar la ventana y el equilibrio con jugadores.

## Escenario espacial y mira vertical

Domain guarda Player.Pitch, Projectile.Height y GameWorld.CeilingHeight. GameInput añade Look y CenterView con valores por defecto para conservar llamadas anteriores. Application aplica esas entradas, comprueba la altura del impacto y finaliza la misión del puente al agotarse sus 300 segundos. Cambiar solo el renderizado no puede provocar una victoria.

Infrastructure genera la geometría nueva mediante ShipLayoutFactory y coloca objetivos alcanzables mediante MissionPlanFactory. Presentation lee la inclinación para proyectar muros, piso, techo y sprites; ShipVisuals genera y conserva texturas y panoramas. Los objetivos tienen siluetas distintas (válvula, antena, carga y pilón).
