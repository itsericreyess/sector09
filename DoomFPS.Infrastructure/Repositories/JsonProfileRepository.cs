using System.Text.Json;
using System.Text.Json.Serialization;
using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;

namespace DoomFPS.Infrastructure.Repositories;

// Perfiles locales guardados en un archivo JSON en el equipo del jugador,
// como las cuentas locales de una consola: sin correo, sin contraseña, solo
// un nombre. "admin" (sin distinguir mayúsculas) es siempre administrador.
public sealed class JsonProfileRepository : IProfileRepository
{
    private sealed class Store
    {
        public List<PlayerProfile> Profiles { get; set; } = new();
        public string? LastUsedName { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string path;
    private Store store;

    public JsonProfileRepository(string? overridePath = null)
    {
        path = overridePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Sector09", "perfiles.json");
        store = Load();
    }

    public IReadOnlyList<PlayerProfile> All => store.Profiles
        .OrderByDescending(p => p.LastPlayedAt).ToList();

    public string? LastUsedName => store.LastUsedName;

    public PlayerProfile? Find(string name)
        => store.Profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public PlayerProfile CreateOrGet(string name)
    {
        name = name.Trim();
        var existing = Find(name);
        if (existing is not null) return existing;
        var profile = new PlayerProfile
        {
            Name = name,
            Role = string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase)
                ? ProfileRole.Administrador : ProfileRole.Jugador,
            CreatedAt = DateTimeOffset.UtcNow,
            LastPlayedAt = DateTimeOffset.UtcNow
        };
        store.Profiles.Add(profile);
        Persist();
        return profile;
    }

    public void Save(PlayerProfile profile)
    {
        int index = store.Profiles.FindIndex(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) store.Profiles[index] = profile; else store.Profiles.Add(profile);
        Persist();
    }

    public void Delete(string name)
    {
        store.Profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(store.LastUsedName, name, StringComparison.OrdinalIgnoreCase)) store.LastUsedName = null;
        Persist();
    }

    public void SetLastUsed(string? name)
    {
        store.LastUsedName = name;
        Persist();
    }

    private Store Load()
    {
        try
        {
            if (!File.Exists(path)) return new Store();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Store>(json, Options) ?? new Store();
        }
        catch
        {
            // Un archivo corrupto o ilegible no debe impedir jugar: se
            // empieza con la lista de perfiles vacía.
            return new Store();
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(store, Options));
        }
        catch
        {
            // Guardar es en el mejor esfuerzo: si el disco falla, se sigue
            // jugando solo que sin progreso persistente.
        }
    }
}
