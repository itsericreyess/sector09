using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Application.Services;

// Hilo conductor de la campaña: encadena las misiones, arrastra el equipo del
// jugador de una a otra y decide cuándo la historia ha terminado.
public sealed class CampaignService
{
    private readonly ILevelSource levels;
    private readonly ICampaignRepository progress;
    private readonly Func<IGameStateRepository> repositoryFactory;

    public DifficultySettings Difficulty { get; private set; } = DifficultySettings.For(Domain.Difficulty.Normal);
    public int LevelIndex { get; private set; }
    public int LevelCount => levels.LevelCount;
    public GameService Game { get; private set; }
    public LevelInfo Info => Game.Level;
    public bool IsFinalLevel => TrainingMode || LevelIndex >= LevelCount - 1;
    public bool TrainingMode { get; private set; }
    public bool CampaignFinished { get; private set; }
    public IReadOnlyList<LevelResult> Results => progress.Results;
    public int TotalScore => progress.Results.Sum(r => r.Score);
    public int TotalKills => progress.Results.Sum(r => r.Kills);
    public int TotalSeconds => progress.Results.Sum(r => r.Seconds);

    public CampaignService(ILevelSource levels, ICampaignRepository progress,
        Func<IGameStateRepository> repositoryFactory)
    {
        this.levels = levels; this.progress = progress; this.repositoryFactory = repositoryFactory;
        Game = Build(0, Loadout.Start(Difficulty));
    }

    // Empieza una campaña nueva desde la primera misión.
    public void StartNewCampaign(Difficulty difficulty)
    {
        TrainingMode = false;
        Difficulty = DifficultySettings.For(difficulty);
        progress.Reset();
        CampaignFinished = false;
        LevelIndex = 0;
        var start = Loadout.Start(Difficulty);
        progress.SaveCheckpoint(0, start);
        Game = Build(0, start);
    }

    // Entrada pública para practicar una misión sin alterar las reglas ni el daño.
    public void StartTraining(int levelIndex, Difficulty difficulty)
    {
        if (levelIndex < 0 || levelIndex >= LevelCount) throw new ArgumentOutOfRangeException(nameof(levelIndex));
        Difficulty = DifficultySettings.For(difficulty);
        TrainingMode = true; CampaignFinished = false; LevelIndex = levelIndex; progress.Reset();
        var player = new Player(); player.ClearWeapons(); player.GiveWeapon(WeaponKind.Pistola);
        player.GiveWeapon(WeaponKind.Escopeta);
        if (levelIndex >= 1) player.GiveWeapon(WeaponKind.Ametralladora);
        if (levelIndex >= 2) player.GiveWeapon(WeaponKind.EscopetaDoble);
        if (levelIndex >= 4) player.GiveWeapon(WeaponKind.Plasma);
        if (levelIndex >= 5) player.GiveWeapon(WeaponKind.Lanzacohetes);
        player.AddAmmo(AmmoKind.Bullets, 180); player.AddAmmo(AmmoKind.Shells, 70);
        player.AddAmmo(AmmoKind.Cells, 180); player.AddAmmo(AmmoKind.Rockets, 25);
        player.AddArmor(50, 100);
        var equipment = player.Capture(); progress.SaveCheckpoint(levelIndex, equipment);
        Game = Build(levelIndex, equipment);
    }

    // Punto de guardado para reanudar más tarde: la misión actual con el
    // equipo del último punto de control y el resumen de misiones ya hechas.
    public CampaignSave Snapshot()
    {
        var loadout = progress.LoadCheckpoint(LevelIndex) ?? Game.Player.Capture();
        return new CampaignSave(LevelIndex, Difficulty.Level, loadout, TotalScore,
            Results.ToList(), DateTimeOffset.UtcNow);
    }

    // Reconstruye la campaña a partir de un guardado anterior: misión, equipo
    // y resumen de misiones completadas.
    public void Resume(CampaignSave save)
    {
        TrainingMode = false;
        CampaignFinished = false;
        Difficulty = DifficultySettings.For(save.Difficulty);
        progress.Reset();
        foreach (var result in save.Results) progress.RegisterLevelResult(result);
        LevelIndex = Math.Clamp(save.LevelIndex, 0, LevelCount - 1);
        progress.SaveCheckpoint(LevelIndex, save.Loadout);
        Game = Build(LevelIndex, save.Loadout);
    }

    // Reintentar la misión actual con el equipo que se tenía al empezarla.
    public void RetryLevel()
    {
        var checkpoint = progress.LoadCheckpoint(LevelIndex) ?? Loadout.Start(Difficulty);
        CampaignFinished = false;
        Game = Build(LevelIndex, checkpoint);
    }

    // Cierra la misión ganada, guarda su resumen y prepara la siguiente.
    public LevelResult CompleteLevel()
    {
        var result = new LevelResult(LevelIndex, Info.Name, Game.ElapsedFrames / GameService.FramesPerSecond,
            Math.Min(Game.DefeatedEnemies, Game.TotalEnemies), Game.TotalEnemies, Game.Score,
            Game.World.Pickups.Count(p => p.Collected), Game.World.Pickups.Count);
        progress.RegisterLevelResult(result);
        if (IsFinalLevel) CampaignFinished = true;
        return result;
    }

    public bool AdvanceToNextLevel()
    {
        if (IsFinalLevel) { CampaignFinished = true; return false; }
        // Se pasa a la siguiente misión con exactamente lo que se tenía al
        // terminar la anterior: misma vida, misma armadura, misma munición y
        // las mismas armas. Sin botiquín ni recarga de cortesía entre niveles.
        var carried = Game.Player.Capture();
        LevelIndex++;
        progress.SaveCheckpoint(LevelIndex, carried);
        Game = Build(LevelIndex, carried);
        return true;
    }

    private GameService Build(int index, Loadout loadout)
    {
        var level = levels.Create(index, Difficulty);
        var repository = repositoryFactory();
        var game = new GameService(level.World, repository,
            new CombatService(repository), new DoorService(repository),
            level.Info, Difficulty, RequiredKillsFor(index, level), level.Mission);
        game.Player.Apply(loadout);
        return game;
    }

    // Cuántas bajas exige la salida. Las misiones cortas piden limpieza total;
    // las grandes admiten dejar rezagados, y la final exige matar al jefe.
    private static int RequiredKillsFor(int index, Level level)
    {
        int total = level.World.Enemies.Count;
        if (level.Info.IsBoss) return (int)(total * 0.60);
        if (index <= 1) return total;
        return (int)Math.Ceiling(total * 0.85);
    }
}
