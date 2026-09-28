namespace DoomFPS.Domain;

// Perfil local, sin correo ni contraseña: solo un nombre de usuario, como las
// cuentas locales de Xbox 360. Se guarda en disco (ver IProfileRepository)
// para que la lista de jugadores y el progreso sobrevivan a cerrar el juego.
public enum ProfileRole { Jugador, Administrador }

// Punto de guardado de una campaña a medio terminar: permite reanudar donde
// se dejó en vez de reiniciar la historia.
public sealed record CampaignSave(
    int LevelIndex, Difficulty Difficulty, Loadout Loadout, int Score,
    IReadOnlyList<LevelResult> Results, DateTimeOffset SavedAt);

public sealed class PlayerProfile
{
    public string Name { get; set; } = "";
    public ProfileRole Role { get; set; } = ProfileRole.Jugador;
    public int CampaignsCompleted { get; set; }
    public int BestScore { get; set; }
    public int BestLevelReached { get; set; }
    public int TotalSecondsPlayed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastPlayedAt { get; set; }
    public CampaignSave? Save { get; set; }

    // Un jugador normal no puede elegir misión libremente hasta completar la
    // campaña al menos una vez. El administrador siempre puede.
    public bool CanSelectAnyLevel => Role == ProfileRole.Administrador || CampaignsCompleted > 0;
}
