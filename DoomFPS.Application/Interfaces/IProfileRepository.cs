using DoomFPS.Domain;

namespace DoomFPS.Application.Interfaces;

// Perfiles locales de jugador: sin correo ni contraseña, solo un nombre de
// usuario que se puede crear y elegir como en las cuentas locales de una
// consola. La implementación de Infrastructure los guarda en disco.
public interface IProfileRepository
{
    IReadOnlyList<PlayerProfile> All { get; }
    PlayerProfile? Find(string name);
    PlayerProfile CreateOrGet(string name);
    void Save(PlayerProfile profile);
    void Delete(string name);
    string? LastUsedName { get; }
    void SetLastUsed(string? name);
}
