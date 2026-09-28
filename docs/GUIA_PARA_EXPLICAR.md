# Guion para exponer

Nuestro proyecto es un juego de disparos inspirado en Doom y organizado por capas. Cada capa tiene una responsabilidad para que las reglas se puedan probar sin abrir la ventana del juego.

En Domain están el jugador, los enemigos, las armas y las puertas. También están el plan y el estado de la misión. Aquí se representa, por ejemplo, si una válvula ya está cerrada o si se completaron los interruptores en el orden correcto.

Application coordina las acciones. GameService recibe las entradas y organiza la simulación; CombatService resuelve daño, DoorService atiende puertas y MissionService coordina los objetivos. CampaignService permite avanzar, repetir una misión o entrar al modo práctica.

Infrastructure crea los mapas y guarda el estado en memoria. Application usa interfaces para pedir ese estado: no depende de una implementación concreta. Presentation captura teclado y ratón y dibuja lo que ocurrió. Por ejemplo, al pulsar E la ventana envía la acción, el servicio verifica la distancia y el dominio actualiza el objetivo; después la pantalla muestra el progreso.

Las pruebas de humo son una revisión rápida de lo esencial. Comprobamos que se crea una campaña, se cargan los siete niveles, funcionan movimiento y disparo, se puede interactuar, pausar, reintentar y seleccionar una práctica. Si esto falla, primero arreglamos esa base.

Las pruebas de aceptación verifican requisitos concretos. Un ejemplo es: dado un corredor eléctrico, cuando intento activar el segundo interruptor antes del primero, el progreso no debe avanzar. Otro es: dado un jefe protegido, cuando le disparo antes de apagar los pilones, su salud no debe disminuir. También probamos las consecuencias negativas: perder por tiempo o detener el dron al abandonarlo.

Usamos xUnit para organizar casos y comprobar resultados. Algunas unitarias utilizan Moq para sustituir el repositorio y verificar llamadas. Las nuevas pruebas de aceptación conectan los servicios y el repositorio real en memoria sobre escenarios controlados. Humo y aceptación indican el propósito del caso; unitarias e integración indican cómo se aíslan o conectan los componentes.

En esta revisión se ejecutaron 104 pruebas y todas aprobaron. La ejecución usó xUnit y Moq reales mediante un ejecutor por reflexión. La prueba manual de la ventana en Windows queda pendiente. No afirmamos que un resultado automático demuestre por sí solo que todos los niveles están equilibrados o que el usuario haya aceptado el juego.

Para la demostración abrimos Casos_practicos.bat, ejecutamos humo y luego aceptación. Consultamos la guía para el caso seleccionado y abrimos Jugar.bat. Con F3 elegimos el nivel, usamos M para localizar objetivos y E para interactuar. Anotamos el resultado observado y adjuntamos una captura. Si no coincide con el resultado esperado, registramos el fallo.

Ahora cada nivel cambia la forma de avanzar: activar relés, cruzar electricidad y ordenar interruptores, escoltar, contener una fuga, defender una transmisión, escapar tras colocar cargas y quitar el escudo del jefe. El combate acompaña esos objetivos, pero ya no se exige repetir una cuota de bajas en todos los niveles.

La nueva historia sucede dentro de una nave averiada que se dirige hacia un planeta. Cada cubierta cambia tanto su plano como sus materiales y objetivo. Los ventanales permiten ver el espacio, pero son parte del casco y no se pueden atravesar. Al final tenemos cinco minutos para recuperar el puente y cambiar la trayectoria.

También podemos mover la mira arriba y abajo. Esa entrada pasa de la pantalla a Application y actualiza la inclinación del jugador en Domain. Los disparos usan esa altura: si apuntamos al techo, podemos fallar al enemigo y el proyectil choca arriba. Lo comprobamos con nuevos casos de aceptación, además de revisar visualmente la cámara.
