# language: es
Característica: Inicio de campaña de Sector 09
  Como jugador
  Quiero iniciar una campaña nueva
  Para comenzar la misión de la estación espacial en condiciones válidas

  Escenario: Iniciar campaña en dificultad normal
    Dado que el jugador selecciona la dificultad normal
    Cuando inicia una campaña nueva
    Entonces la campaña debe contener 7 niveles
    Y el jugador debe iniciar con vida
    Y el estado del juego debe ser Playing
