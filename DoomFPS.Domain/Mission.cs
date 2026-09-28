namespace DoomFPS.Domain;

public enum MissionKind { Relays, PowerGrid, Escort, Containment, Uplink, Demolition, BossShield }

public sealed record MissionPoint(string Name, double X, double Y);
public sealed record MissionPlan(MissionKind Kind, IReadOnlyList<MissionPoint> Points,
    MissionPoint Exit, IReadOnlyList<MissionPoint> Hazards, int DurationSeconds = 240,
    int HoldSeconds = 18);

public sealed class MissionNode
{
    public MissionPoint Point { get; }
    public bool Activated { get; private set; }
    public MissionNode(MissionPoint point) => Point = point;
    public void Activate() => Activated = true;
}

// Estado y reglas de progreso. No conoce teclado, ventanas o repositorios.
public sealed class MissionState
{
    public MissionPlan Plan { get; }
    public IReadOnlyList<MissionNode> Nodes { get; }
    public int Completed => Nodes.Count(n => n.Activated);
    public int ElapsedFrames { get; private set; }
    public int ClockFrames { get; private set; }
    public int HoldFrames { get; private set; }
    public bool Started { get; private set; }
    public bool EscortArrived { get; private set; }
    public double DroneX { get; private set; }
    public double DroneY { get; private set; }
    public bool DroneMoving { get; set; }
    public bool Contested { get; set; }
    public bool Failed { get; private set; }
    public bool Complete => Plan.Kind switch
    {
        MissionKind.Escort => EscortArrived,
        MissionKind.Uplink => HoldFrames >= Plan.HoldSeconds * 60,
        _ => Completed == Nodes.Count
    };
    public bool ShieldActive => Plan.Kind == MissionKind.BossShield && !Complete;
    public bool ElectricityOn => Plan.Kind == MissionKind.PowerGrid && !Complete && ElapsedFrames % 180 < 100;
    public bool Timed => Plan.Kind == MissionKind.Containment && !Complete
        || Plan.Kind == MissionKind.Demolition && Complete || Plan.Kind == MissionKind.BossShield;
    public int RemainingSeconds => Math.Max(0, (Plan.DurationSeconds * 60 - ClockFrames + 59) / 60);
    public double Progress => Plan.Kind == MissionKind.Uplink ? Math.Min(1, HoldFrames / (double)(Plan.HoldSeconds * 60))
        : Plan.Kind == MissionKind.Escort ? (EscortArrived ? 1 : Started ? 0.5 : 0)
        : Completed / (double)Math.Max(1, Nodes.Count);

    public MissionState(MissionPlan plan)
    {
        if (plan.Points.Count == 0 || plan.DurationSeconds <= 0 || plan.HoldSeconds <= 0)
            throw new ArgumentException("Una misión necesita objetivos y tiempos positivos.");
        Plan = plan;
        Nodes = Array.AsReadOnly(plan.Points.Select(p => new MissionNode(p)).ToArray());
        DroneX = plan.Points[0].X; DroneY = plan.Points[0].Y;
    }

    public string Activate(int index)
    {
        if (Failed || index < 0 || index >= Nodes.Count) return "Objetivo no disponible.";
        if (Nodes[index].Activated) return "Este objetivo ya está activado.";
        if (Plan.Kind == MissionKind.PowerGrid && index != Completed)
            return $"Orden incorrecto. Activa primero el interruptor {Completed + 1}.";
        Nodes[index].Activate(); Started = true;
        return Plan.Kind switch
        {
            MissionKind.Escort => "Dron encendido. Acompáñalo; se detiene si te alejas o hay peligro.",
            MissionKind.Uplink => "Transmisión iniciada. Permanece en el círculo y evita intrusos.",
            MissionKind.Demolition when Complete => $"¡Separación armada! Sal del reactor en {RemainingSeconds} s.",
            MissionKind.BossShield when Complete => "Escudo desactivado. ¡El Ciberdemonio es vulnerable!",
            _ => $"{Nodes[index].Point.Name}: {Completed}/{Nodes.Count}."
        };
    }

    public void Tick(bool uploading)
    {
        if (Failed) return;
        ElapsedFrames++;
        if (Timed && ++ClockFrames >= Plan.DurationSeconds * 60) Failed = true;
        if (Plan.Kind == MissionKind.Uplink && Started && uploading && !Complete) HoldFrames++;
    }

    public void MoveDrone(double x, double y)
    {
        DroneX = x; DroneY = y;
        if (Math.Sqrt(Math.Pow(x - Plan.Exit.X, 2) + Math.Pow(y - Plan.Exit.Y, 2)) < 0.30)
            EscortArrived = true;
    }

    public string Title => Plan.Kind switch
    {
        MissionKind.Relays => "RESTABLECE LA SEÑAL",
        MissionKind.PowerGrid => "CRUZA LA RED ELÉCTRICA",
        MissionKind.Escort => "ESCOLTA AL DRON",
        MissionKind.Containment => "SELLA LA FUGA",
        MissionKind.Uplink => "DEFIENDE LA TRANSMISIÓN",
        MissionKind.Demolition => "SEPARA EL REACTOR",
        _ => "RECUPERA EL PUENTE"
    };
    public string Detail => Failed ? "TIEMPO AGOTADO" : Plan.Kind switch
    {
        MissionKind.Escort => EscortArrived ? "Dron a salvo. Pisa la salida."
            : !Started ? "Acércate al dron y pulsa E."
            : Contested ? "Hay enemigos cerca del dron. Despeja el camino."
            : DroneMoving ? "Dron avanzando. Mantente cerca."
            : "Acércate al dron y abre las puertas de su ruta.",
        MissionKind.Uplink => Complete ? "Datos enviados. Busca la salida."
            : !Started ? "Activa la antena con E."
            : $"Envío {HoldFrames * 100 / (Plan.HoldSeconds * 60)}% · " + (Contested ? "zona disputada" : "permanece cerca"),
        MissionKind.PowerGrid => Complete ? "Electricidad apagada. Ve a la salida."
            : $"Interruptor {Completed + 1}/{Nodes.Count} · suelo " + (ElectricityOn ? "ACTIVO: espera" : "SEGURO: cruza"),
        MissionKind.Containment => Complete ? "Fuga sellada. Ve a la salida." : $"Válvulas {Completed}/{Nodes.Count} · {RemainingSeconds} s",
        MissionKind.Demolition => Complete ? $"¡EVACÚA! {RemainingSeconds} s" : $"Cargas {Completed}/{Nodes.Count} · coloca con E",
        MissionKind.BossShield => Complete ? $"IMPACTO EN {RemainingSeconds} s · elimina al jefe y llega al timón" : $"Pilones {Completed}/{Nodes.Count} · IMPACTO EN {RemainingSeconds} s",
        _ => Complete ? "Señal recuperada. Ve a la salida." : $"Relés {Completed}/{Nodes.Count} · activa con E"
    };
}
