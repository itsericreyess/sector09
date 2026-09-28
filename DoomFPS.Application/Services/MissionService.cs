using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Application.Services;

// Orquesta los objetivos usando la geometría y las reglas del dominio.
public sealed class MissionService
{
    public MissionState State { get; }
    private int hazardCooldown;
    public MissionService(MissionPlan plan) => State = new(plan);

    public string? Interact(Player player, GameWorld world)
    {
        for (int i = 0; i < State.Nodes.Count; i++)
        {
            var point = State.Nodes[i].Point;
            double dx = point.X - player.X, dy = point.Y - player.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > 1.4 || !world.HasLineOfSight(player.X, player.Y, point.X, point.Y)) continue;
            if (distance > 0.15 && (dx * Math.Cos(player.Angle) + dy * Math.Sin(player.Angle)) / distance < 0.65) continue;
            return State.Activate(i);
        }
        return null;
    }

    // Devuelve daño del entorno. Solo GameService aplica daño al jugador.
    public int Tick(Player player, GameWorld world)
    {
        if (hazardCooldown > 0) hazardCooldown--;
        var point = State.Plan.Points[0];
        bool near = Distance(player.X, player.Y, point.X, point.Y) <= 2.2
            && world.HasLineOfSight(player.X, player.Y, point.X, point.Y);
        State.Contested = world.Enemies.Any(e => e.IsAlive && Distance(e.X, e.Y,
            State.Plan.Kind == MissionKind.Escort ? State.DroneX : point.X,
            State.Plan.Kind == MissionKind.Escort ? State.DroneY : point.Y) < 2.6);
        State.Tick(near && !State.Contested);
        if (State.Plan.Kind == MissionKind.Escort) Escort(player, world);
        if (State.ElectricityOn && hazardCooldown == 0 && State.Plan.Hazards.Any(h =>
            (int)h.X == (int)player.X && (int)h.Y == (int)player.Y))
        { hazardCooldown = 40; return 6; }
        return 0;
    }

    private void Escort(Player player, GameWorld world)
    {
        State.DroneMoving = false;
        if (!State.Started || State.Complete || State.Contested ||
            Distance(player.X, player.Y, State.DroneX, State.DroneY) > 3.5
            || !world.HasLineOfSight(player.X, player.Y, State.DroneX, State.DroneY)) return;
        var start = ((int)State.DroneX, (int)State.DroneY);
        var goal = ((int)State.Plan.Exit.X, (int)State.Plan.Exit.Y);
        var previous = new Dictionary<(int, int), (int, int)> { [start] = start };
        var queue = new Queue<(int, int)>(); queue.Enqueue(start);
        while (queue.Count > 0 && !previous.ContainsKey(goal))
        {
            var cell = queue.Dequeue();
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (cell.Item1 + d.Item1, cell.Item2 + d.Item2);
                // Planificar a través de puertas; detenerse ante ellas hasta que el jugador abra.
                if (previous.ContainsKey(next) || GameWorld.SolidMaterials.Contains(world.Tile(next.Item1, next.Item2))) continue;
                previous[next] = cell; queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(goal)) return;
        var target = goal;
        while (previous[target] != start && target != start) target = previous[target];
        double tx = target.Item1 + 0.5, ty = target.Item2 + 0.5;
        // Centrar primero el dron en su celda antes de doblar en un pasillo.
        if (target.Item1 != start.Item1 && Math.Abs(State.DroneY - (start.Item2 + 0.5)) > 0.025)
        { tx = start.Item1 + 0.5; ty = start.Item2 + 0.5; }
        if (target.Item2 != start.Item2 && Math.Abs(State.DroneX - (start.Item1 + 0.5)) > 0.025)
        { tx = start.Item1 + 0.5; ty = start.Item2 + 0.5; }
        double distance = Distance(State.DroneX, State.DroneY, tx, ty);
        if (distance < 0.001) { State.MoveDrone(tx, ty); return; }
        double step = Math.Min(distance, 1.8 / 60);
        double nx = State.DroneX + (tx - State.DroneX) / distance * step;
        double ny = State.DroneY + (ty - State.DroneY) / distance * step;
        if (!world.CanStand(nx, ny, 0.15)) return;
        State.MoveDrone(nx, ny); State.DroneMoving = true;
    }

    private static double Distance(double x, double y, double xx, double yy)
        => Math.Sqrt((x - xx) * (x - xx) + (y - yy) * (y - yy));
}
