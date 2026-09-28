using DoomFPS.Domain.Entities;

namespace DoomFPS.Domain;

// Estados posibles de una misión. LevelComplete y CampaignComplete son de la campaña.
public enum GameStatus { Playing, Paused, Won, Lost, LevelComplete, CampaignComplete }

// Objetos recogibles. Los primeros cuatro existían en la versión original.
public enum PickupKind
{
    RedKey, BlueKey, Ammo, Health,
    YellowKey, Armor, MegaHealth, Bullets, Cells, Rockets, Backpack,
    WeaponShotgun, WeaponDoubleShotgun, WeaponMachinegun, WeaponPlasma, WeaponRocket
}

// Tipos de munición. La escopeta usa cartuchos, igual que el arma original.
public enum AmmoKind { Shells, Bullets, Cells, Rockets }

// Las seis armas de la campaña.
public enum WeaponKind { Pistola, Escopeta, EscopetaDoble, Ametralladora, Plasma, Lanzacohetes }

// Cada especie tiene aspecto, animación, movimiento y ataque propios.
public enum EnemyKind { Imp, Cacodemon, Zombi, Demonio, Espectro, AlmaPerdida, CaballeroInfernal, Mancubus, Ciberdemonio }

// Patrón de desplazamiento. Chaser reproduce exactamente la IA de la versión original.
public enum EnemyBehavior { Chaser, Zigzag, Charger, Circler, Kamikaze, Floater, Sentinel, Boss }

// Forma de atacar de cada especie.
public enum AttackKind { Melee, Hitscan, Projectile, Burst, Explode, BossVolley }

// Fase de animación del enemigo. El renderizador dibuja un sprite distinto por fase.
public enum EnemyState { Idle, Walk, WindUp, Attack, Pain, Dead }

// Aspecto visual de cada proyectil.
public enum ProjectileKind { Fireball, Plasma, Rocket, Soul, Slug, GreenBolt }

// Partículas decorativas: sangre, chispas, humo y explosiones.
public enum ParticleKind { Blood, Spark, Smoke, Fire, Gib }

public enum Difficulty { Facil, Normal, Pesadilla }

// Ajustes que multiplican la dificultad. Se aplican al construir cada nivel.
public sealed record DifficultySettings(
    Difficulty Level, string Name, string Description,
    double EnemyHealth, double EnemyDamage, double EnemySpeed,
    double EnemyCooldown, double SupplyFactor, int StartHealth)
{
    public static DifficultySettings For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Facil => new(Difficulty.Facil, "RECLUTA",
            "Los demonios pegan menos y aguantan menos. Recomendado para aprender el sector.",
            0.75, 0.60, 0.90, 1.30, 1.45, 100),
        Difficulty.Pesadilla => new(Difficulty.Pesadilla, "PESADILLA",
            "Más vida, más daño y más velocidad enemiga. La munición escasea.",
            1.40, 1.55, 1.18, 0.70, 0.70, 100),
        _ => new(Difficulty.Normal, "VETERANO",
            "El equilibrio previsto para la campaña. Duro pero justo.",
            1.00, 1.00, 1.00, 1.00, 1.00, 100)
    };
}

public readonly record struct GameInput(double Forward = 0, double Strafe = 0,
    double Turn = 0, bool Fire = false, bool Interact = false, bool Sprint = false,
    int WeaponSlot = 0, bool NextWeapon = false, bool PreviousWeapon = false,
    double Look = 0, bool CenterView = false, bool Reload = false, bool Jump = false);

public sealed record DoorPlacement(int X, int Y, Door Door);

public readonly record struct RayHit(double Distance, double X, double Y,
    int CellX, int CellY, int Side, char Material);

public sealed record GameEvent(string Kind, string Message);

// Una línea de diálogo narrado en subtítulos, sin voz. GameService la muestra
// en pantalla con su interlocutor (p. ej. "CONTROL") durante unos segundos.
public sealed record DialogueLine(string Speaker, string Text);

// Ficha narrativa y de presentación de un nivel.
public sealed record LevelInfo(
    int Number, string Name, string Subtitle,
    IReadOnlyList<string> Briefing, string Debrief,
    double SpawnX, double SpawnY, double SpawnAngle,
    bool IsBoss = false, int ParSeconds = 180, string Theme = "estacion",
    IReadOnlyList<DialogueLine>? ComsChatter = null);

// Un nivel listo para jugarse: geometría más su ficha.
public sealed class Level
{
    public LevelInfo Info { get; }
    public GameWorld World { get; }
    public MissionPlan? Mission { get; }
    public Level(LevelInfo info, GameWorld world, MissionPlan? mission = null) { Info = info; World = world; Mission = mission; }
}

// Lo que el jugador conserva al pasar de una misión a la siguiente.
public sealed record Loadout(
    int Health, int Armor, IReadOnlyDictionary<AmmoKind, int> Ammo,
    IReadOnlyList<WeaponKind> Weapons, WeaponKind Current, int BackpackLevel = 0)
{
    public static Loadout Start(DifficultySettings settings) => new(
        settings.StartHealth, 0,
        new Dictionary<AmmoKind, int> { [AmmoKind.Bullets] = 50, [AmmoKind.Shells] = 0, [AmmoKind.Cells] = 0, [AmmoKind.Rockets] = 0 },
        new[] { WeaponKind.Pistola }, WeaponKind.Pistola);
}

public sealed class Pickup
{
    public PickupKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public bool Collected { get; private set; }
    public Pickup(PickupKind kind, double x, double y) { Kind = kind; X = x; Y = y; }
    public void Collect() => Collected = true;

    public bool IsWeapon => Kind is PickupKind.WeaponShotgun or PickupKind.WeaponDoubleShotgun
        or PickupKind.WeaponMachinegun or PickupKind.WeaponPlasma or PickupKind.WeaponRocket;

    public WeaponKind AsWeapon => Kind switch
    {
        PickupKind.WeaponShotgun => WeaponKind.Escopeta,
        PickupKind.WeaponDoubleShotgun => WeaponKind.EscopetaDoble,
        PickupKind.WeaponMachinegun => WeaponKind.Ametralladora,
        PickupKind.WeaponPlasma => WeaponKind.Plasma,
        _ => WeaponKind.Lanzacohetes
    };
}

// Resultado de una misión terminada: alimenta el resumen entre niveles.
public sealed record LevelResult(int LevelIndex, string LevelName, int Seconds,
    int Kills, int TotalEnemies, int Score, int ItemsFound, int ItemsTotal);
