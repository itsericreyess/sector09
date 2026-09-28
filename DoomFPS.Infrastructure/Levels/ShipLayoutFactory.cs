using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Infrastructure.Levels;

// Cada cubierta se arma como los cuartos de una casa: se entra por un cuarto
// de llegada (la zona segura de verdad: un cuarto entero sin enemigos, no
// solo unos segundos de gracia), y desde ahí se avanza a los cuartos
// contiguos por pasillos. Algunas cubiertas encierran un cuarto pequeño
// dentro de uno grande (como el baño dentro de una habitación) y varían en
// cuánto casco acristalado tienen: unas están llenas de ventanales al
// espacio, otras casi no tienen ninguno.
public static class ShipLayoutFactory
{
    private readonly record struct Room(int X, int Y, int W, int H)
    {
        public int CenterX => X + W / 2;
        public int CenterY => Y + H / 2;
        public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
    }

    public static (GameWorld World, LevelInfo Info) Build(int index, List<Enemy> enemies, List<Pickup> supplies)
    {
        const int width = 32, height = 27;
        var grid = Enumerable.Range(0, height).Select(_ => Enumerable.Repeat('#', width).ToArray()).ToArray();

        void Rect(int x, int y, int w, int h, char c = '.')
        { for (int yy = y; yy < y + h; yy++) for (int xx = x; xx < x + w; xx++) grid[yy][xx] = c; }

        // Pasillo en "L" de dos celdas de ancho entre los centros de dos
        // cuartos: conecta cualquier par de cuartos, compartan pared o no.
        void Link(Room a, Room b)
        {
            int ax = a.CenterX, ay = a.CenterY, bx = b.CenterX, by = b.CenterY;
            Rect(Math.Min(ax, bx), ay - 1, Math.Abs(bx - ax) + 1, 2);
            Rect(bx - 1, Math.Min(ay, by), 2, Math.Abs(by - ay) + 1);
        }

        // Un cuarto pequeño queda encerrado dentro de otro más grande, con
        // una sola puerta: la lógica de "el baño dentro de la habitación".
        void Nest(int px, int py, int pw, int ph, int doorX, int doorY)
        {
            Rect(px - 1, py - 1, pw + 2, ph + 2, '#');
            Rect(px, py, pw, ph, '.');
            grid[doorY][doorX] = '.';
        }

        Room spawnRoom = default, exitRoom = default;
        var rooms = new List<Room>();
        var links = new List<(Room A, Room B)>();
        bool manyWindows;

        switch (index)
        {
            case 0: // Hangar de acoplamiento: la primera zona segura de la nave.
                spawnRoom = new(11, 18, 10, 7);
                var bay = new Room(8, 9, 16, 8);
                var leftBay = new Room(2, 10, 5, 6);
                var rightBay = new Room(24, 10, 6, 6);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, bay, leftBay, rightBay, exitRoom });
                links.AddRange(new[] { (spawnRoom, bay), (bay, leftBay), (bay, rightBay), (bay, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Nest(17, 11, 4, 3, 16, 12); // sala de control encerrada en la bahía principal
                Rect(10, 13, 2, 3, '&');
                manyWindows = true;
                break;

            case 1: // Sala de energía: pasillos estrechos entre generadores.
                spawnRoom = new(2, 19, 6, 6);
                var corridorA = new Room(2, 12, 6, 5);
                var hub = new Room(9, 12, 14, 4);
                var sideRoom = new Room(24, 12, 6, 5);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, corridorA, hub, sideRoom, exitRoom });
                links.AddRange(new[] { (spawnRoom, corridorA), (corridorA, hub), (hub, sideRoom), (hub, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Rect(10, 13, 3, 1, '&'); Rect(19, 13, 3, 1, '&');
                manyWindows = false;
                break;

            case 2: // Bodega de carga: nave larga con contenedores desfasados.
                spawnRoom = new(12, 18, 8, 7);
                var warehouse = new Room(2, 9, 28, 7);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, warehouse, exitRoom });
                links.AddRange(new[] { (spawnRoom, warehouse), (warehouse, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Rect(5, 10, 3, 2, '='); Rect(11, 12, 3, 2, '=');
                Rect(18, 10, 3, 2, '='); Rect(24, 12, 3, 2, '=');
                manyWindows = true;
                break;

            case 3: // Refrigeración: tanques y corredores en cruz.
                spawnRoom = new(11, 18, 10, 7);
                var crossHub = new Room(13, 12, 6, 5);
                var westTank = new Room(4, 11, 6, 7);
                var eastTank = new Room(22, 11, 6, 7);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, crossHub, westTank, eastTank, exitRoom });
                links.AddRange(new[] { (spawnRoom, crossHub), (crossHub, westTank), (crossHub, eastTank), (crossHub, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Rect(5, 12, 4, 5, '%'); Rect(23, 12, 4, 5, '%');
                manyWindows = false;
                break;

            case 4: // Observatorio orbital: cúpula acristalada con dos cuartos de instrumentos encerrados.
                spawnRoom = new(11, 18, 10, 7);
                var dome = new Room(2, 8, 28, 9);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, dome, exitRoom });
                links.AddRange(new[] { (spawnRoom, dome), (dome, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Nest(9, 10, 4, 3, 13, 11);
                Nest(19, 12, 4, 3, 18, 13);
                Rect(14, 15, 3, 1, '=');
                manyWindows = true;
                break;

            case 5: // Reactor: núcleo cercado con una ruta de circunvalación.
                spawnRoom = new(11, 18, 10, 7);
                var ring = new Room(7, 9, 18, 8);
                exitRoom = new(12, 2, 8, 6);
                rooms.AddRange(new[] { spawnRoom, ring, exitRoom });
                links.AddRange(new[] { (spawnRoom, ring), (ring, exitRoom) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Rect(12, 11, 8, 4, '%');
                manyWindows = false;
                break;

            default: // Puente de mando: proa ancha y completamente acristalada.
                spawnRoom = new(11, 18, 10, 7);
                var approach = new Room(10, 11, 12, 6);
                var bridge = new Room(2, 2, 28, 8);
                exitRoom = bridge;
                rooms.AddRange(new[] { spawnRoom, approach, bridge });
                links.AddRange(new[] { (spawnRoom, approach), (approach, bridge) });
                foreach (var r in rooms) Rect(r.X, r.Y, r.W, r.H);
                foreach (var (a, b) in links) Link(a, b);
                Rect(9, 5, 5, 2, '='); Rect(18, 5, 5, 2, '='); Rect(13, 7, 3, 2, '=');
                manyWindows = true;
                break;
        }

        grid[2][15] = 'X';

        // Ventanales: las cubiertas "abiertas" recorren buena parte del
        // casco acristalado; las cerradas apenas dejan un par de mirillas.
        // Cada fila/columna usada aquí cae dentro de un cuarto ya abierto
        // de esa cubierta, así la ventana se ve de verdad desde dentro.
        void Window(int x, int y) { if (grid[y][x] != 'X') grid[y][x] = 'W'; }
        Window(15, 1); Window(16, 1); // siempre hay alguna vista al espacio sobre la salida
        if (manyWindows) Window(13, 1);
        switch (index)
        {
            case 0: Window(1, 11); Window(1, 13); Window(30, 11); Window(30, 13); break;
            case 2: Window(1, 10); Window(1, 13); Window(30, 10); Window(30, 13); break;
            case 4: Window(1, 10); Window(1, 14); Window(30, 10); Window(30, 14); break;
            default:
                if (index == 6) { Window(1, 4); Window(1, 7); Window(30, 4); Window(30, 7); }
                break;
        }

        var cells = new List<(int X, int Y)>();
        for (int y = 3; y < height - 4; y++)
            for (int x = 3; x < width - 3; x++)
                if (grid[y][x] == '.') cells.Add((x, y));

        var random = new Random(909 + index);
        for (int i = cells.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }
        var occupied = new HashSet<(int, int)>();

        // La zona segura es un cuarto de verdad: ningún enemigo puede
        // aparecer dentro de él, no solo "lejos" del punto de llegada.
        var enemyCells = cells.Where(c => !spawnRoom.Contains(c.X, c.Y)).ToList();
        foreach (var enemy in enemies)
        {
            var pool = enemyCells.Count > 0 ? enemyCells : cells;
            var c = pool.First(c => !occupied.Contains(c));
            if (enemy.Kind == EnemyKind.Ciberdemonio) c = (exitRoom.CenterX, exitRoom.CenterY + 2);
            enemy.SetPosition(c.X + 0.5, c.Y + 0.5); occupied.Add(c);
        }
        var pickups = new List<Pickup>();
        foreach (var supply in supplies.Where(p => p.Kind is not (PickupKind.RedKey or PickupKind.BlueKey or PickupKind.YellowKey)))
        {
            var c = cells.First(c => !occupied.Contains(c));
            occupied.Add(c); pickups.Add(new(supply.Kind, c.X + 0.5, c.Y + 0.5));
        }
        // Suministros visibles en la propia zona segura: nadie sale desarmado.
        pickups.Add(new(PickupKind.Health, spawnRoom.CenterX - 1.5, spawnRoom.CenterY + 0.5));
        pickups.Add(new(PickupKind.Bullets, spawnRoom.CenterX + 1.5, spawnRoom.CenterY + 0.5));
        pickups.Add(new(PickupKind.WeaponShotgun, spawnRoom.CenterX + 0.5, spawnRoom.CenterY - 0.5));

        string[] names = { "HANGAR DE ACOPLAMIENTO", "SALA DE ENERGÍA", "BODEGA DE CARGA", "REFRIGERACIÓN", "OBSERVATORIO ORBITAL", "REACTOR INESTABLE", "PUENTE DE MANDO" };
        string[] themes = { "hangar", "energia", "bodega", "refrigeracion", "observatorio", "reactor", "puente" };
        string[] summaries = { "Comunicaciones recuperadas. Restablece ahora la energía de la nave.",
            "Red estabilizada. Lleva el dron de reparación al siguiente módulo.", "Dron a salvo. El reactor aún pierde refrigerante.",
            "Fuga sellada. Envía una nueva trayectoria desde el observatorio.", "Ruta orbital calculada. Hay que separar el reactor dañado.",
            "Reactor expulsado al espacio. Recupera el timón antes del impacto.", "Motores activados. Trayectoria corregida: nave y planeta a salvo." };
        var info = new LevelInfo(index + 1, names[index], $"SECTOR 09 · Cubierta {index + 1} / 7",
            MissionPlanFactory.Briefing((MissionKind)index), summaries[index], index == 1 ? 3.5 : 15.5, 23.5, -Math.PI / 2,
            index == 6, 240 + index * 30, themes[index]);
        return (new GameWorld(grid.Select(r => new string(r)).ToArray(), Array.Empty<DoorPlacement>(), enemies, pickups, index == 6 ? 3.4 : 2.2), info);
    }
}
