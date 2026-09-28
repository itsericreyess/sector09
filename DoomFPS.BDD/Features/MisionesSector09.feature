# language: es
Característica: Progreso de campaña, práctica y reintentos en Sector 09
  Como jugador de Sector 09
  Quiero que la campaña conserve mi progreso y respete las reglas de cada misión
  Para completar la historia de rescate de la nave sin perder equipo injustamente

  @REQ-CAMP-01
  Escenario: Iniciar campaña en dificultad Pesadilla
    Dado una campaña nueva preparada en dificultad Pesadilla
    Entonces la misión activa debe ser la número 1
    Y la dificultad aplicada debe ser PESADILLA
    Y el jugador debe tener 100 puntos de salud

  @REQ-CAMP-02
  Escenario: Reintentar una misión restablece el equipo inicial
    Dado una campaña nueva preparada en dificultad Normal
    Cuando el jugador recibe 45 puntos de daño
    Y el jugador reintenta la misión
    Entonces el jugador debe tener 100 puntos de salud
    Y la misión debe tener 0 objetivos completados

  @REQ-CAMP-03
  Escenario: Pasar a la siguiente misión conserva el equipo
    Dado una campaña nueva preparada en dificultad Normal
    Cuando el jugador recibe 20 puntos de daño
    Y el jugador avanza a la siguiente misión
    Entonces la misión activa debe ser la número 2
    Y el jugador debe tener 80 puntos de salud

  @REQ-PRAC-01
  Escenario: El modo práctica carga la misión final del puente de mando
    Dado que el jugador abre el modo práctica en la misión 7
    Entonces la misión activa debe ser la número 7
    Y el modo práctica debe estar activo
    Y el jugador debe tener al menos 2 armas

  @REQ-PRAC-02
  Escenario: El modo práctica rechaza una misión inexistente
    Dado una campaña nueva preparada en dificultad Normal
    Cuando el jugador intenta practicar la misión 8
    Entonces el sistema debe rechazar la solicitud por rango inválido

  @REQ-SAVE-01
  Escenario: Guardar y reanudar la campaña conserva la misión alcanzada
    Dado una campaña nueva preparada en dificultad Normal
    Y el jugador avanza a la siguiente misión
    Cuando se guarda la partida y se reanuda en una sesión nueva
    Entonces la misión activa debe ser la número 2
    Y el modo práctica no debe estar activo

  @REQ-GAME-01
  Escenario: La pausa congela el reloj de la misión
    Dado una campaña nueva preparada en dificultad Normal
    Cuando el jugador pausa la partida y transcurren 120 cuadros
    Entonces el reloj de la misión debe marcar 0 cuadros
    Y la partida debe quedar en estado Paused
