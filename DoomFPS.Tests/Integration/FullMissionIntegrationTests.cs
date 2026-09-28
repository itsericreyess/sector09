using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using Xunit;

namespace DoomFPS.Tests.Integration;

public class FullMissionIntegrationTests
{
    [Fact]
    public void NivelReal_RecogerTarjetas_AbrirPuertas_EliminarEnemigos_LlegarASalida()
    {
        // Bot de prueba: se mueve con GameInput y dispara con el servicio real.
        // No teletransporta, no entrega llaves, no cambia la vida y no usa mocks.
        var game = TestFactory.Game(StationLevelFactory.Create());
        WalkTo(game, 3, 16);
        Assert.Contains("Tarjeta Roja", game.Player.Inventory);
        WalkTo(game, 5, 9); Open(game, 6, 9);
        WalkTo(game, 9, 2);
        Assert.Contains("Tarjeta Azul", game.Player.Inventory);
        WalkTo(game, 10, 15);
        WalkTo(game, 11, 9); Open(game, 12, 9);
        WalkTo(game, 16, 14);
        WalkTo(game, 16, 2);
        Assert.Equal(6, game.DefeatedEnemies);
        Assert.True(game.World.Doors.All(d => d.Door.IsOpen));
        Assert.Equal(GameStatus.Won, game.Status);
    }

    private static void Open(GameService game, int x, int y)
    {
        TestFactory.Aim(game, x + 0.5, y + 0.5);
        game.Step(new(Interact: true));
        Assert.True(game.World.DoorAt(x, y)!.IsOpen);
    }

    private static void FightVisible(GameService game)
    {
        for (int ticks = 0; ticks < 600; ticks++)
        {
            var p = game.Player;
            var target = game.World.Enemies.Where(e => e.IsAlive
                && Math.Sqrt(Math.Pow(e.X - p.X, 2) + Math.Pow(e.Y - p.Y, 2)) < 7
                && game.World.HasLineOfSight(p.X, p.Y, e.X, e.Y))
                .OrderBy(e => Math.Pow(e.X - p.X, 2) + Math.Pow(e.Y - p.Y, 2)).FirstOrDefault();
            if (target is null) return;
            TestFactory.Aim(game, target.X, target.Y); game.Step(new(Fire: true));
            Assert.True(game.Player.IsAlive, "El bot murió durante el recorrido.");
        }
        Assert.Fail("El combate del recorrido no terminó.");
    }

    private static void WalkTo(GameService game, int targetX, int targetY)
    {
        var start = ((int)game.Player.X, (int)game.Player.Y);
        var goal = (targetX, targetY);
        var previous = new Dictionary<(int, int), (int, int)> { [start] = start };
        var queue = new Queue<(int, int)>(); queue.Enqueue(start);
        (int, int)[] directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0 && !previous.ContainsKey(goal))
        {
            var cell = queue.Dequeue();
            foreach (var d in directions)
            {
                var next = (cell.Item1 + d.Item1, cell.Item2 + d.Item2);
                if (previous.ContainsKey(next) || game.World.IsSolid(next.Item1, next.Item2)) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        Assert.True(previous.ContainsKey(goal), "No hay camino a la meta.");
        var path = new List<(int, int)> { goal };
        while (path[^1] != start) path.Add(previous[path[^1]]);
        path.Reverse();
        foreach (var cell in path)
        {
            bool reached = false;
            for (int tick = 0; tick < 180; tick++)
            {
                FightVisible(game);
                double dx = cell.Item1 + 0.5 - game.Player.X, dy = cell.Item2 + 0.5 - game.Player.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 0.08 || game.Status == GameStatus.Won) { reached = true; break; }
                TestFactory.Aim(game, cell.Item1 + 0.5, cell.Item2 + 0.5);
                game.Step(new(Forward: 1));
            }
            Assert.True(reached, $"No se alcanzó la celda {cell}.");
        }
    }
}
