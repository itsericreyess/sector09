using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Infrastructure.Levels;

public static class MissionPlanFactory
{
    public static MissionPlan Create(GameWorld world, LevelInfo info)
    {
        var kind = (MissionKind)(info.Number - 1);
        var start = ((int)info.SpawnX, (int)info.SpawnY);
        var distance = new Dictionary<(int, int), int> { [start] = 0 };
        var queue = new Queue<(int, int)>(); queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (cell.Item1 + d.Item1, cell.Item2 + d.Item2);
                if (distance.ContainsKey(next) || world.IsSolid(next.Item1, next.Item2)) continue;
                distance[next] = distance[cell] + 1; queue.Enqueue(next);
            }
        }
        var exit = distance.Keys.First(c => world.Tile(c.Item1, c.Item2) == 'X');
        bool Clear((int, int) c) => world.Enemies.All(e => Math.Abs(e.X - (c.Item1 + 0.5)) + Math.Abs(e.Y - (c.Item2 + 0.5)) > 1.2)
            && world.Pickups.All(p => (int)p.X != c.Item1 || (int)p.Y != c.Item2);
        var candidates = distance.Keys.Where(c => distance[c] >= 3 && c != exit && Clear(c))
            .OrderBy(c => distance[c]).ThenBy(c => c.Item2).ThenBy(c => c.Item1).ToList();
        int count = kind is MissionKind.Escort or MissionKind.Uplink ? 1 : kind == MissionKind.Relays ? 2 : 3;
        var points = new List<MissionPoint>();
        for (int i = 0; i < count; i++)
        {
            double fraction = kind == MissionKind.Escort ? 0.02 : kind == MissionKind.Uplink ? 0.55 : (i + 1d) / (count + 1);
            var cell = candidates[(int)((candidates.Count - 1) * fraction)];
            string label = kind switch
            {
                MissionKind.Relays => "RELÉ", MissionKind.PowerGrid => "INTERRUPTOR",
                MissionKind.Escort => "DRON", MissionKind.Containment => "VÁLVULA",
                MissionKind.Uplink => "ANTENA", MissionKind.Demolition => "CARGA", _ => "PILAR"
            };
            points.Add(new(label + " " + (i + 1), cell.Item1 + 0.5, cell.Item2 + 0.5));
        }
        var hazards = kind == MissionKind.PowerGrid
            ? distance.Keys.Where(c => (c.Item2 is 8 or 10) && points.All(p => (int)p.X != c.Item1 || (int)p.Y != c.Item2))
                .Select(c => new MissionPoint("ALTA TENSIÓN", c.Item1 + 0.5, c.Item2 + 0.5)).ToArray()
            : Array.Empty<MissionPoint>();
        return new(kind, points, new("SALIDA", exit.Item1 + 0.5, exit.Item2 + 0.5), hazards,
            kind == MissionKind.Demolition ? 60 : kind == MissionKind.BossShield ? 300 : 240, 18);
    }

    public static IReadOnlyList<string> Briefing(MissionKind kind) => kind switch
    {
        MissionKind.Relays => new[] { "La Sector 09 cae hacia el planeta: reactiva los dos relés del hangar.",
            "Acércate, mira el panel y pulsa E. Puedes elegir el orden.", "Con ambos relés activos, alcanza la salida; no necesitas matar a todos." },
        MissionKind.PowerGrid => new[] { "El pasillo eléctrico alterna rojo peligroso y azul seguro.",
            "Activa los interruptores 1, 2 y 3 en ese orden con E.", "Los tres cortan la corriente y habilitan la salida. Espera antes de cruzar." },
        MissionKind.Escort => new[] { "Activa el dron con E y acompáñalo hasta la salida.",
            "Avanza solo si estás cerca y no hay enemigos junto a él.", "Despeja la ruta. Si te alejas, el dron se detiene y te espera." },
        MissionKind.Containment => new[] { "El refrigerante se agota: el reactor explotará en cuatro minutos.",
            "Cierra las tres válvulas marcadas con E, en cualquier orden.", "La última detiene la cuenta atrás. Después busca la salida." },
        MissionKind.Uplink => new[] { "Envía la nueva ruta orbital: activa la antena y mantén la posición.",
            "Necesita 18 segundos de transmisión: un enemigo cerca la interrumpe.", "Puedes salir a combatir sin perder lo enviado. Al completar, evacúa." },
        MissionKind.Demolition => new[] { "Arma tres cargas con E para separar el reactor dañado de la nave.",
            "Al colocar la última comienza una cuenta atrás de 60 segundos.", "Abandona el módulo en 60 s: la detonación lo expulsará al espacio." },
        _ => new[] { "¡Impacto contra el planeta en 300 s! El jefe ha bloqueado el timón.",
            "Desactiva los tres pilares con E mientras esquivas sus ataques.", "Derrótalo y llega al timón verde para encender motores y salvar la nave." }
    };
}
