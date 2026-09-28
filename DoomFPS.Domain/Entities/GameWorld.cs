namespace DoomFPS.Domain.Entities;

// Geometría y reglas del mundo sin dependencias de gráficos o persistencia.
public sealed class GameWorld
{
    private readonly string[] map;
    private readonly Dictionary<(int, int), Door> doorIndex;
    private readonly List<Enemy> enemies;
    private readonly List<Pickup> pickups;

    public IReadOnlyList<DoorPlacement> Doors { get; }
    public IReadOnlyList<Enemy> Enemies => enemies;
    public IReadOnlyList<Pickup> Pickups => pickups;
    public int Width => map[0].Length;
    public int Height => map.Length;
    public double CeilingHeight { get; }

    // Muros: piedra, placas metálicas, roca infernal y columnas técnicas.
    public const string SolidMaterials = "#=%&W";

    public GameWorld(string[] map, IEnumerable<DoorPlacement> doors,
        IEnumerable<Enemy> enemies, IEnumerable<Pickup> pickups, double ceilingHeight = 1)
    {
        if (map.Length < 3 || map.Any(r => r.Length != map[0].Length))
            throw new ArgumentException("El mapa debe ser rectangular.", nameof(map));
        if (!double.IsFinite(ceilingHeight) || ceilingHeight < 1) throw new ArgumentOutOfRangeException(nameof(ceilingHeight));
        CeilingHeight = ceilingHeight;
        this.map = (string[])map.Clone();
        Doors = Array.AsReadOnly(doors.ToArray());
        this.enemies = enemies.ToList();
        this.pickups = pickups.ToList();
        doorIndex = Doors.ToDictionary(d => (d.X, d.Y), d => d.Door);
    }

    // El jefe invoca almas perdidas durante el combate.
    public void AddEnemy(Enemy enemy) => enemies.Add(enemy);
    public void AddPickup(Pickup pickup) => pickups.Add(pickup);

    public char Tile(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? '#' : map[y][x];
    public Door? DoorAt(int x, int y) => doorIndex.GetValueOrDefault((x, y));

    public static bool IsDoorTile(char tile) => tile is 'R' or 'B' or 'Y';

    public bool IsSolid(int x, int y)
    {
        char tile = Tile(x, y);
        return SolidMaterials.Contains(tile) || (IsDoorTile(tile) && DoorAt(x, y)?.IsOpen != true);
    }

    public bool CanStand(double x, double y, double radius = 0.2)
    {
        return !IsSolid((int)Math.Floor(x - radius), (int)Math.Floor(y - radius))
            && !IsSolid((int)Math.Floor(x + radius), (int)Math.Floor(y - radius))
            && !IsSolid((int)Math.Floor(x - radius), (int)Math.Floor(y + radius))
            && !IsSolid((int)Math.Floor(x + radius), (int)Math.Floor(y + radius));
    }

    // DDA recorre celdas exactas. El mismo cálculo evita disparos a través de muros.
    public RayHit CastRay(double x, double y, double dx, double dy, double maxDistance = 40)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(dx)
            || !double.IsFinite(dy) || (dx == 0 && dy == 0) || maxDistance <= 0)
            throw new ArgumentOutOfRangeException(nameof(dx));
        int cellX = (int)Math.Floor(x), cellY = (int)Math.Floor(y);
        double deltaX = dx == 0 ? double.PositiveInfinity : Math.Abs(1 / dx);
        double deltaY = dy == 0 ? double.PositiveInfinity : Math.Abs(1 / dy);
        int stepX = dx < 0 ? -1 : 1, stepY = dy < 0 ? -1 : 1;
        double sideX = dx == 0 ? double.PositiveInfinity : (dx < 0 ? x - cellX : cellX + 1 - x) * deltaX;
        double sideY = dy == 0 ? double.PositiveInfinity : (dy < 0 ? y - cellY : cellY + 1 - y) * deltaY;
        int side = 0; double distance = 0;
        for (int i = 0; i < 512; i++)
        {
            if (IsSolid(cellX, cellY))
                return new(Math.Max(distance, 0.0001), x + dx * distance, y + dy * distance,
                    cellX, cellY, side, Tile(cellX, cellY));
            if (sideX < sideY) { distance = sideX; sideX += deltaX; cellX += stepX; side = 0; }
            else { distance = sideY; sideY += deltaY; cellY += stepY; side = 1; }
            if (distance >= maxDistance) break;
        }
        return new(maxDistance, x + dx * maxDistance, y + dy * maxDistance, cellX, cellY, side, '.');
    }

    public bool HasLineOfSight(double x, double y, double targetX, double targetY)
    {
        double dx = targetX - x, dy = targetY - y, distance = Math.Sqrt(dx * dx + dy * dy);
        return distance < 0.001 || CastRay(x, y, dx / distance, dy / distance, distance).Distance >= distance - 0.001;
    }
}
