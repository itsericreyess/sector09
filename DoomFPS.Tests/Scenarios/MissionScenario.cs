using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Repositories;

namespace DoomFPS.Tests.Scenarios;

// Preparación controlada de escenarios. Todos los servicios y repositorios son reales.
// La posición inicial se prepara en Given; la acción siempre pasa por GameInput.
internal static class MissionScenario
{
    public static GameService Create(MissionKind kind, int seconds = 240, int hold = 2,
        IEnumerable<Enemy>? enemies = null)
    {
        var world = new GameWorld(new[] {
            "##############", "#............#", "#............#", "#............#",
            "#............#", "#...........X#", "#............#", "##############"
        }, Array.Empty<DoorPlacement>(), enemies ?? Array.Empty<Enemy>(), Array.Empty<Pickup>());
        var points = new[] { new MissionPoint("OBJETIVO 1", 4.5, 2.5),
            new MissionPoint("OBJETIVO 2", 8.5, 2.5), new MissionPoint("OBJETIVO 3", 10.5, 4.5) };
        int count = kind is MissionKind.Escort or MissionKind.Uplink ? 1 : kind == MissionKind.Relays ? 2 : 3;
        var plan = new MissionPlan(kind, points.Take(count).ToArray(), new("SALIDA", 12.5, 5.5),
            kind == MissionKind.PowerGrid ? new[] { new MissionPoint("TENSIÓN", 3.5, 4.5) } : Array.Empty<MissionPoint>(), seconds, hold);
        var repo = new InMemoryGameStateRepository();
        return new(world, repo, new CombatService(repo), new DoorService(repo), mission: plan);
    }
    public static void PrepareAtNode(GameService game, int index)
    {
        var p = game.Mission!.Nodes[index].Point;
        // El plano puede tener una pared a la izquierda del panel: elegir un lado libre.
        var offset = new[] { (-0.8, 0d), (0.8, 0d), (0d, -0.8), (0d, 0.8) }
            .First(o => game.World.CanStand(p.X + o.Item1, p.Y + o.Item2));
        game.Player.SetPosition(p.X + offset.Item1, p.Y + offset.Item2);
        game.Player.Rotate(Math.Atan2(-offset.Item2, -offset.Item1) - game.Player.Angle);
    }
    public static void UseNode(GameService game, int index)
    { PrepareAtNode(game, index); game.Step(new(Interact: true)); }
    public static void UseAll(GameService game)
    { for (int i = 0; i < game.Mission!.Nodes.Count; i++) UseNode(game, i); }
    public static void EnterExit(GameService game)
    { var p = game.Mission!.Plan.Exit; game.Player.SetPosition(p.X, p.Y); game.Step(default); }
    public static void Steps(GameService game, int count)
    { for (int i = 0; i < count; i++) game.Step(default); }
    public static void WalkStep(GameService game, double x, double y)
    {
        game.Player.Rotate(Math.Atan2(y - game.Player.Y, x - game.Player.X) - game.Player.Angle);
        game.Step(new(Forward: 1));
    }
}
