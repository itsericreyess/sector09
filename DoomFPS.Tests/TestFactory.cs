using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Repositories;

namespace DoomFPS.Tests;

internal static class TestFactory
{
    public static GameWorld EmptyWorld(IEnumerable<Enemy>? enemies = null, IEnumerable<Pickup>? pickups = null)
        => new(new[] { "########", "#......#", "#......#", "#......#", "#......#", "#.....X#", "########" },
            Array.Empty<DoorPlacement>(), enemies ?? Array.Empty<Enemy>(), pickups ?? Array.Empty<Pickup>());
    public static GameService Game(GameWorld world)
    {
        var repo = new InMemoryGameStateRepository();
        return new(world, repo, new CombatService(repo), new DoorService(repo));
    }
    public static Enemy EnemyAt(double x, double y, int health = 60)
    { var enemy = new Enemy { Health = health }; enemy.SetPosition(x, y); return enemy; }
    public static void Aim(GameService game, double x, double y)
        => game.Player.Rotate(Math.Atan2(y - game.Player.Y, x - game.Player.X) - game.Player.Angle);
}
