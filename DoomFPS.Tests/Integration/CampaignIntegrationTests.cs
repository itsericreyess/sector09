using DoomFPS.Application.Interfaces;
using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using Xunit;

namespace DoomFPS.Tests.Integration;

// La campaña completa: siete misiones encadenadas, cada una jugable de verdad.
public class CampaignIntegrationTests
{
    private static readonly DifficultySettings Normal = DifficultySettings.For(Difficulty.Normal);

    private static CampaignService NewCampaign(Difficulty difficulty = Difficulty.Normal)
    {
        var campaign = new CampaignService(new CampaignLevelFactory(),
            new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        campaign.StartNewCampaign(difficulty);
        return campaign;
    }

    [Fact]
    public void LaCampanaTieneSieteMisionesYLaUltimaEsLaDelJefe()
    {
        var levels = new CampaignLevelFactory();
        Assert.Equal(7, levels.LevelCount);
        for (int i = 0; i < levels.LevelCount; i++)
        {
            var level = levels.Create(i, Normal);
            Assert.Equal(i + 1, level.Info.Number);
            Assert.False(string.IsNullOrWhiteSpace(level.Info.Name));
            Assert.NotEmpty(level.Info.Briefing);
        }
        Assert.True(levels.Create(6, Normal).Info.IsBoss);
        Assert.Contains(levels.Create(6, Normal).World.Enemies,
            e => e.Kind == EnemyKind.Ciberdemonio);
    }

    [Fact]
    public void CadaMisionTieneMasEnemigosQueLaAnterior()
    {
        var levels = new CampaignLevelFactory();
        int previous = 0;
        for (int i = 0; i < 6; i++)   // la séptima es un jefe, no una horda
        {
            int count = levels.Create(i, Normal).World.Enemies.Count;
            Assert.True(count > previous, $"El nivel {i + 1} no sube la dificultad: {count} <= {previous}.");
            previous = count;
        }
    }

    [Fact]
    public void LaVarietyDeEnemigosCreceALoLargoDeLaCampana()
    {
        var levels = new CampaignLevelFactory();
        int first = levels.Create(0, Normal).World.Enemies.Select(e => e.Kind).Distinct().Count();
        int last = levels.Create(5, Normal).World.Enemies.Select(e => e.Kind).Distinct().Count();
        Assert.True(last > first);
        var all = new HashSet<EnemyKind>();
        for (int i = 0; i < levels.LevelCount; i++)
            foreach (var enemy in levels.Create(i, Normal).World.Enemies) all.Add(enemy.Kind);
        Assert.Equal(9, all.Count);   // las nueve especies aparecen en la campaña
    }

    [Fact]
    public void EnCadaMisionSePuedenAlcanzarLlaves_Puertas_YSalida()
    {
        var levels = new CampaignLevelFactory();
        for (int i = 0; i < levels.LevelCount; i++)
        {
            var level = levels.Create(i, Normal);
            var world = level.World;
            var start = ((int)level.Info.SpawnX, (int)level.Info.SpawnY);
            Assert.False(world.IsSolid(start.Item1, start.Item2), $"Nivel {i + 1}: el inicio está dentro de un muro.");

            // Se recogen las llaves alcanzables y se repite hasta que no haya novedades.
            var keys = new HashSet<string>();
            bool changed = true;
            HashSet<(int, int)> seen = new();
            while (changed)
            {
                changed = false;
                seen = Flood(world, start, keys);
                foreach (var pickup in world.Pickups)
                {
                    string? key = KeyName(pickup.Kind);
                    if (key is null || keys.Contains(key)) continue;
                    if (!seen.Contains(((int)pickup.X, (int)pickup.Y))) continue;
                    keys.Add(key); changed = true;
                }
            }
            foreach (var door in world.Doors)
                Assert.True(keys.Contains(door.Door.RequiredKey),
                    $"Nivel {i + 1}: la {door.Door.Name} no se puede abrir nunca.");

            var exit = Cells(world).First(c => world.Tile(c.Item1, c.Item2) == 'X');
            Assert.True(seen.Contains(exit), $"Nivel {i + 1}: la salida no es alcanzable.");
            foreach (var enemy in world.Enemies)
                Assert.True(seen.Contains(((int)enemy.X, (int)enemy.Y)),
                    $"Nivel {i + 1}: un {enemy.Type} quedó encerrado.");
        }
    }

    [Fact]
    public void PasarDeMisionConservaExactamenteElEquipoDeLaMisionAnterior()
    {
        var campaign = NewCampaign();
        campaign.Game.Player.GiveWeapon(WeaponKind.Escopeta);
        campaign.Game.Player.TakeDamage(80);       // queda con 20 de salud
        int healthBefore = campaign.Game.Player.Health;
        int armorBefore = campaign.Game.Player.Armor;
        var ammoBefore = campaign.Game.Player.Capture().Ammo;
        campaign.CompleteLevel();
        Assert.True(campaign.AdvanceToNextLevel());
        Assert.Equal(1, campaign.LevelIndex);
        Assert.True(campaign.Game.Player.HasWeapon(WeaponKind.Escopeta));
        // Sin botiquín ni recarga de cortesía entre misiones: se llega con lo
        // mismo con lo que se terminó la anterior.
        Assert.Equal(healthBefore, campaign.Game.Player.Health);
        Assert.Equal(armorBefore, campaign.Game.Player.Armor);
        foreach (var pair in ammoBefore)
            Assert.Equal(pair.Value, campaign.Game.Player.AmmoOf(pair.Key));
    }

    [Fact]
    public void ReintentarDevuelveElEquipoDelInicioDeLaMision()
    {
        var campaign = NewCampaign();
        campaign.CompleteLevel();
        campaign.AdvanceToNextLevel();
        int healthAtStart = campaign.Game.Player.Health;
        campaign.Game.Player.TakeDamage(healthAtStart - 1);
        campaign.RetryLevel();
        Assert.Equal(1, campaign.LevelIndex);
        Assert.Equal(healthAtStart, campaign.Game.Player.Health);
        Assert.Equal(0, campaign.Game.ElapsedFrames);
    }

    [Fact]
    public void ElResumenDeCadaMisionSeGuardaEnElRepositorio()
    {
        var campaign = NewCampaign();
        var result = campaign.CompleteLevel();
        Assert.Equal(0, result.LevelIndex);
        Assert.Equal(campaign.Game.TotalEnemies, result.TotalEnemies);
        Assert.Single(campaign.Results);
        Assert.Equal(result.Score, campaign.TotalScore);
    }

    [Fact]
    public void LaSalidaDelJefeExigeMatarAlCiberdemonio()
    {
        var campaign = NewCampaign();
        for (int i = 0; i < 6; i++) { campaign.CompleteLevel(); campaign.AdvanceToNextLevel(); }
        var game = campaign.Game;
        Assert.True(game.Level.IsBoss);
        var boss = game.World.Enemies.First(e => e.Kind == EnemyKind.Ciberdemonio);

        // Con el jefe vivo, pisar la salida no gana la misión.
        var exit = Cells(game.World).First(c => game.World.Tile(c.Item1, c.Item2) == 'X');
        game.Player.SetPosition(exit.Item1 + 0.5, exit.Item2 + 0.5);
        game.Step(new GameInput());
        Assert.Equal(GameStatus.Playing, game.Status);

        // El criterio nuevo exige desactivar pilares además de derrotar al jefe.
        for (int i = 0; i < game.Mission!.Nodes.Count; i++) DoomFPS.Tests.Scenarios.MissionScenario.UseNode(game, i);
        foreach (var enemy in game.World.Enemies) enemy.TakeDamage(enemy.Health);
        Assert.False(boss.IsAlive);
        game.Player.SetPosition(exit.Item1 + 0.5, exit.Item2 + 0.5);
        game.Step(new GameInput());
        Assert.Equal(GameStatus.Won, game.Status);
    }

    [Fact]
    public void LaPrimeraMisionSeCompletaConUnBotQueCaminaYDispara()
    {
        var campaign = NewCampaign(Difficulty.Facil);
        var game = campaign.Game;
        var player = game.Player;
        player.GiveWeapon(WeaponKind.Escopeta);
        player.AddAmmo(AmmoKind.Shells, 60);
        player.AddAmmo(AmmoKind.Bullets, 200);

        foreach (var pickup in game.World.Pickups)
        {
            if (KeyName(pickup.Kind) is null) continue;
            Walk(game, (int)pickup.X, (int)pickup.Y);
        }
        foreach (var door in game.World.Doors)
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (game.World.IsSolid(door.X + dx, door.Y + dy)) continue;
                Walk(game, door.X + dx, door.Y + dy);
                Aim(game, door.X + 0.5, door.Y + 0.5);
                game.Step(new GameInput(Interact: true));
                if (door.Door.IsOpen) break;
            }
            Assert.True(door.Door.IsOpen, $"El bot no pudo abrir la {door.Door.Name}.");
        }
        for (int round = 0; round < 40 && game.World.Enemies.Any(e => e.IsAlive); round++)
        {
            var target = game.World.Enemies.Where(e => e.IsAlive)
                .OrderBy(e => Distance(player, e)).First();
            Walk(game, (int)target.X, (int)target.Y);
        }
        Assert.Equal(game.TotalEnemies, game.DefeatedEnemies);
        // Activar los relés caminando hasta los paneles reales del nivel.
        foreach (var node in game.Mission!.Nodes)
        {
            Walk(game, (int)node.Point.X, (int)node.Point.Y);
            Aim(game, node.Point.X, node.Point.Y);
            game.Step(new GameInput(Interact: true));
        }
        Assert.True(game.Mission.Complete);

        var exit = Cells(game.World).First(c => game.World.Tile(c.Item1, c.Item2) == 'X');
        Walk(game, exit.Item1, exit.Item2);
        Assert.Equal(GameStatus.Won, game.Status);
    }

    // ------------------------------------------------------------------ ayudas

    private static string? KeyName(PickupKind kind) => kind switch
    {
        PickupKind.RedKey => "Tarjeta Roja",
        PickupKind.BlueKey => "Tarjeta Azul",
        PickupKind.YellowKey => "Tarjeta Amarilla",
        _ => null
    };

    private static IEnumerable<(int, int)> Cells(GameWorld world)
    {
        for (int y = 0; y < world.Height; y++)
            for (int x = 0; x < world.Width; x++)
                yield return (x, y);
    }

    private static HashSet<(int, int)> Flood(GameWorld world, (int, int) start, HashSet<string> keys)
    {
        var seen = new HashSet<(int, int)> { start };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);
        (int, int)[] directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in directions)
            {
                var next = (cell.Item1 + d.Item1, cell.Item2 + d.Item2);
                if (seen.Contains(next)) continue;
                var door = world.DoorAt(next.Item1, next.Item2);
                if (door is not null)
                {
                    if (!keys.Contains(door.RequiredKey)) continue;
                }
                else if (world.IsSolid(next.Item1, next.Item2)) continue;
                seen.Add(next); queue.Enqueue(next);
            }
        }
        return seen;
    }

    private static double Distance(Player player, Enemy enemy)
        => Math.Sqrt(Math.Pow(enemy.X - player.X, 2) + Math.Pow(enemy.Y - player.Y, 2));

    private static void Aim(GameService game, double x, double y)
        => game.Player.Rotate(Math.Atan2(y - game.Player.Y, x - game.Player.X) - game.Player.Angle);

    // Camina hasta una celda peleando por el camino. El bot es inmortal: aquí se
    // comprueba que la misión se puede terminar, no que sea fácil.
    private static void Walk(GameService game, int targetX, int targetY)
    {
        var path = Path(game.World, ((int)game.Player.X, (int)game.Player.Y), (targetX, targetY));
        if (path is null) return;
        foreach (var cell in path)
        {
            for (int tick = 0; tick < 240; tick++)
            {
                if (game.Status != GameStatus.Playing) return;
                game.Player.Health = 100;
                game.Player.AddAmmo(AmmoKind.Shells, 2);
                var target = game.World.Enemies.Where(e => e.IsAlive
                        && Distance(game.Player, e) < 8
                        && game.World.HasLineOfSight(game.Player.X, game.Player.Y, e.X, e.Y))
                    .OrderBy(e => Distance(game.Player, e)).FirstOrDefault();
                if (target is not null)
                {
                    Aim(game, target.X, target.Y);
                    // El cargador ya no se rellena solo: si no hay balas cargadas,
                    // el bot debe recargar a mano antes de poder volver a disparar.
                    var weapon = game.Player.CurrentWeapon;
                    bool needsReload = weapon.MagazineSize > 0 && weapon.Loaded < weapon.AmmoPerShot;
                    game.Step(new GameInput(Fire: !needsReload, Reload: needsReload, WeaponSlot: 2));
                    continue;
                }
                double dx = cell.Item1 + 0.5 - game.Player.X, dy = cell.Item2 + 0.5 - game.Player.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 0.16) break;
                Aim(game, cell.Item1 + 0.5, cell.Item2 + 0.5);
                game.Step(new GameInput(Forward: 1, Sprint: true));
            }
        }
    }

    private static List<(int, int)>? Path(GameWorld world, (int, int) start, (int, int) goal)
    {
        var previous = new Dictionary<(int, int), (int, int)> { [start] = start };
        var queue = new Queue<(int, int)>();
        queue.Enqueue(start);
        (int, int)[] directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0 && !previous.ContainsKey(goal))
        {
            var cell = queue.Dequeue();
            foreach (var d in directions)
            {
                var next = (cell.Item1 + d.Item1, cell.Item2 + d.Item2);
                if (previous.ContainsKey(next) || world.IsSolid(next.Item1, next.Item2)) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(goal)) return null;
        var path = new List<(int, int)> { goal };
        while (path[^1] != start) path.Add(previous[path[^1]]);
        path.Reverse();
        path.RemoveAt(0);
        return path;
    }
}
