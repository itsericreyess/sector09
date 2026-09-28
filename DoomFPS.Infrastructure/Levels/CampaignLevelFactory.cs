using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Infrastructure.Levels;

// Los siete niveles de la campaña. Cada mapa es arte ASCII: los muros son
// caracteres sólidos y las letras y dígitos son marcadores que esta fábrica
// convierte en enemigos, objetos y compuertas antes de entregar el mundo.
//
//   #  muro de piedra      =  placa metálica     %  roca infernal   &  columna
//   R B Y  compuertas      X  salida             .  suelo
//   1 zombi   2 imp   3 demonio   4 espectro   5 alma perdida
//   6 cacodemon   7 caballero infernal   8 mancubus   9 ciberdemonio
//   r b y  tarjetas        h botiquín  H megaesfera  a armadura  m mochila
//   s cartuchos  c balas   e celdas    k cohetes
//   S escopeta   D recortada  M ametralladora  P plasma  L lanzacohetes
public sealed class CampaignLevelFactory : ILevelSource
{
    public int LevelCount => Maps.Length;

    public Level Create(int index, DifficultySettings settings)
    {
        int i = Math.Clamp(index, 0, Maps.Length - 1);
        return Build(Maps[i], Infos[i], settings);
    }

    // Recorre el arte ASCII una sola vez: saca las entidades y deja el mapa limpio.
    private static Level Build(string[] source, LevelInfo info, DifficultySettings settings)
    {
        var map = new string[source.Length];
        var enemies = new List<Enemy>();
        var pickups = new List<Pickup>();
        var doors = new List<DoorPlacement>();
        for (int y = 0; y < source.Length; y++)
        {
            char[] row = source[y].PadRight(source.Max(r => r.Length), '#').ToCharArray();
            for (int x = 0; x < row.Length; x++)
            {
                char cell = row[x];
                double cx = x + 0.5, cy = y + 0.5;
                EnemyKind? enemy = EnemyFor(cell);
                if (enemy is not null)
                {
                    enemies.Add(EnemyCatalog.At(enemy.Value, settings, cx, cy));
                    row[x] = '.';
                    continue;
                }
                PickupKind? pickup = PickupFor(cell);
                if (pickup is not null)
                {
                    pickups.Add(new Pickup(pickup.Value, cx, cy));
                    row[x] = '.';
                    continue;
                }
                if (cell == 'R') doors.Add(new DoorPlacement(x, y, new Door("Compuerta Roja", "Tarjeta Roja")));
                else if (cell == 'B') doors.Add(new DoorPlacement(x, y, new Door("Compuerta Azul", "Tarjeta Azul")));
                else if (cell == 'Y') doors.Add(new DoorPlacement(x, y, new Door("Compuerta Amarilla", "Tarjeta Amarilla")));
            }
            map[y] = new string(row);
        }
        var ship = ShipLayoutFactory.Build(info.Number - 1, enemies, pickups);
        var plan = MissionPlanFactory.Create(ship.World, ship.Info);
        return new Level(ship.Info, ship.World, plan);
    }

    private static EnemyKind? EnemyFor(char cell) => cell switch
    {
        '1' => EnemyKind.Zombi,
        '2' => EnemyKind.Imp,
        '3' => EnemyKind.Demonio,
        '4' => EnemyKind.Espectro,
        '5' => EnemyKind.AlmaPerdida,
        '6' => EnemyKind.Cacodemon,
        '7' => EnemyKind.CaballeroInfernal,
        '8' => EnemyKind.Mancubus,
        '9' => EnemyKind.Ciberdemonio,
        _ => null
    };

    private static PickupKind? PickupFor(char cell) => cell switch
    {
        'r' => PickupKind.RedKey,
        'b' => PickupKind.BlueKey,
        'y' => PickupKind.YellowKey,
        'h' => PickupKind.Health,
        'H' => PickupKind.MegaHealth,
        'a' => PickupKind.Armor,
        'm' => PickupKind.Backpack,
        's' => PickupKind.Ammo,
        'c' => PickupKind.Bullets,
        'e' => PickupKind.Cells,
        'k' => PickupKind.Rockets,
        'S' => PickupKind.WeaponShotgun,
        'D' => PickupKind.WeaponDoubleShotgun,
        'M' => PickupKind.WeaponMachinegun,
        'P' => PickupKind.WeaponPlasma,
        'L' => PickupKind.WeaponRocket,
        _ => null
    };

    private static readonly string[][] Maps =
    {
        // ----- Nivel 1: PUESTO DE VIGILANCIA -----
        new[]
        {
            "########################",
            "#...2......#....1......#",
            "#..........#.....X.....#",
            "#....r.....#....1......#",
            "#...2......#...........#",
            "#..........#####R#######",
            "#..a....1..#.....2.....#",
            "#....##....#....##.....#",
            "#....##....#....##...1.#",
            "#.h......s.#....1......#",
            "####.#######......c....#",
            "#.........1............#",
            "#......#........##.....#",
            "#......#...S....##..h..#",
            "#......#........##.....#",
            "#......#...1....##.....#",
            "#......#........##.s...#",
            "########################"
        },
        // ----- Nivel 2: CORREDORES DE SERVICIO -----
        new[]
        {
            "############################",
            "#....1....#......#.........#",
            "#..2...a..#..2...#..2.X....#",
            "#.........#......#.....&...#",
            "#.1..&....#1.....B.........#",
            "#.........#...1..#...h...1.#",
            "#....s....#......#.........#",
            "####.########.##############",
            "#.h........................#",
            "#..1....3.........2....1...#",
            "#.....h...........s........#",
            "######.###############.#####",
            "#......1..#....s.#.........#",
            "#...M.....#...3..#......c..#",
            "#.........R......#....r....#",
            "#....2....#..b...#....3....#",
            "#.........#......#.......2.#",
            "#...c.....#..m...#....h....#",
            "#.........#.1....#.........#",
            "############################"
        },
        // ----- Nivel 3: DEPÓSITO DE CARGA -----
        new[]
        {
            "##############################",
            "#............#.2.............#",
            "#.2......1h..#..=...=.2.=....#",
            "#..=...=...=.#....X....m.....#",
            "#....r.......#.............5.#",
            "#...........4#..=.1.=...=....#",
            "#..=...=...=.#............1..#",
            "#....3.......############.####",
            "#.........2..############B####",
            "######.##################.####",
            "#s..1............4.....2.....#",
            "#......s....3......c....a....#",
            "#.......1....................#",
            "####.###########.#############",
            "#...........#........#.......#",
            "#........1..#.h....4.#.....2.#",
            "#..h........#...D....#..5....#",
            "#...........#........R.......#",
            "#..2........#.1......#...b...#",
            "#........s..#..3.....#....h..#",
            "#.c...1.....#......s.#.4.....#",
            "##############################"
        },
        // ----- Nivel 4: LABORATORIO CONTAMINADO -----
        new[]
        {
            "##############################",
            "#.s...4..#........1.#......1.#",
            "#....2...#.2h.......#..6.....#",
            "#..=...=.#..........#....X...#",
            "#........#.......3..#........#",
            "#.r.....6#...2......#......2.#",
            "#........#####.######..H.....#",
            "#..=...=.#..........#.4......#",
            "#.1......#......s...#........#",
            "####.#########.##########Y####",
            "#..1............6..........4.#",
            "#......s.........e......c....#",
            "#h........3..........2.......#",
            "####.#########################",
            "#........#....2.....#........#",
            "#.....a..#.h........#........#",
            "#..2.....#..%..%5.%.#..2..k..#",
            "#........#..........#........#",
            "#........R..........B........#",
            "#........#.3%..%..%.#......6.#",
            "#..h.....#......b...#.....y..#",
            "#.....1..#.........4#.3......#",
            "#........#...5....c.#.......m#",
            "##############################"
        },
        // ----- Nivel 5: ARSENAL ORBITAL -----
        new[]
        {
            "################################",
            "#.7........#h.....5..#.k..2....#",
            "#.......2..#.6.......#.6..X....#",
            "#.r=..=..=.#.........#..=....=.#",
            "#..........#...2.....Y.........#",
            "#....4.....#.......7.#.......4.#",
            "#.h=..=..=.#..H......#3.=....=.#",
            "#...1....3.#3........#........1#",
            "#####.##########.###############",
            "#s.2...1..h.....7........5...6.#",
            "#.h..s..........e.......a...c..#",
            "#.........3...........4........#",
            "#####.##########################",
            "#1.........#.......e.#........e#",
            "#..........#.2.......#.........#",
            "#..h.......#..&.&.&..#.7..P....#",
            "#..2.......#......5..#........2#",
            "#..........R.........B.........#",
            "#..........#.........#.........#",
            "#..........#..6......#.........#",
            "#..........#..&.&.&..#......y..#",
            "#......3a..#.....b...#.......3.#",
            "#.c........#.h.5...4.#k.5.....m#",
            "################################"
        },
        // ----- Nivel 6: LA COLMENA -----
        new[]
        {
            "################################",
            "#.7......c#.......5..#...k..6..#",
            "#.H.....8.#.6h.......#.8..X....#",
            "#..%..%...#..........#..%..%...#",
            "#.r...2...#........7.#.........#",
            "#........1#.....2....Y...2.....#",
            "#..%.4%...#..3.....h.#..%..%.4.#",
            "#.........#####.######.........#",
            "#.5......3#..........#3........#",
            "#...6.....#..........#.........#",
            "#####.#########.################",
            "#a.....5.........7......5..k.6e#",
            "#.2.&.....&.....&e....&.....&..#",
            "#....s.........................#",
            "#h........3..8..a..2..4........#",
            "#####.##########################",
            "#.........#........e.#........k#",
            "#.....1...#.2........#.7.......#",
            "#..h......#..%..%.5%.#....L....#",
            "#.........#..........#........8#",
            "#..2......R..........B.........#",
            "#.........#..%6.%..%.#.........#",
            "#.........#......b...#......y..#",
            "#......3a.#..........#.......3.#",
            "#.c.......#.h...1..4.#..5.....m#",
            "################################"
        },
        // ----- Nivel 7: EL TRONO DEL CIBERDEMONIO -----
        new[]
        {
            "################################",
            "#..............................#",
            "#.X.2...k..5...h....5.e...2..a.#",
            "#..............................#",
            "####.######################.####",
            "#h.......#............#.......h#",
            "#.6..k...#..7......7..#...e..6.#",
            "#........#............#........#",
            "#..%..%..#..%......%..#..%..%..#",
            "#......5.#.....9......#.5......#",
            "#........#............#........#",
            "#........#............#........#",
            "#.H.2s...#4..........4#...s2.a.#",
            "#........#............#........#",
            "#........#............#........#",
            "#........#............#........#",
            "#.4......#............#......4.#",
            "#..%..%..#..%......%..#..%..%..#",
            "#....r...#............#........#",
            "#........#.k8......8k.#...k....#",
            "#1...e.3.#............#.3.....1#",
            "####.##########R###########.####",
            "#..............................#",
            "#.H...2.....h1...1.c....2......#",
            "#..............................#",
            "################################"
        },
    };

    private static readonly LevelInfo[] Infos =
    {
        new LevelInfo(1, "PUESTO DE VIGILANCIA", "Superficie · Sector 09",
            new[] { "La señal de auxilio del Sector 09 se cortó hace nueve horas.", "Aterrizas solo. La guarnición ya no responde: camina, pero no responde.", "Recupera la tarjeta roja, abre la compuerta y baja a la estación." },
            "El puesto está limpio. Lo que mató a la guarnición vino de abajo.",
            3.5, 14.5, 1.5708,
            false, 150, "estacion"),
        new LevelInfo(2, "CORREDORES DE SERVICIO", "Nivel -1 · Mantenimiento",
            new[] { "Bajo el puesto, las luces de emergencia dibujan sombras que se mueven solas.", "Aquí ya no hay solo cadáveres levantados: algo con brazos largos lanza fuego.", "Dos tarjetas te separan del ascensor de carga." },
            "El ascensor baja. El aire empieza a oler a azufre.",
            4.5, 14.5, 0.0,
            false, 210, "servicio"),
        new LevelInfo(3, "DEPÓSITO DE CARGA", "Nivel -2 · Muelles",
            new[] { "Los manifiestos de carga mencionan un contenedor que nunca se declaró.", "Ese contenedor está abierto. Y algo aprendió a no dejarse ver.", "Consigue la recortada: la vas a necesitar en los pasillos estrechos." },
            "El contenedor era una puerta. Alguien la abrió desde dentro.",
            3.5, 19.5, 0.0,
            false, 260, "carga"),
        new LevelInfo(4, "LABORATORIO CONTAMINADO", "Nivel -3 · Investigación",
            new[] { "Los registros hablan de un portal experimental y de un turno que nunca terminó.", "El reactor sigue encendido, y hay cosas flotando entre los tanques.", "Tres tarjetas cierran el paso al núcleo. Búscalas todas." },
            "El portal no fue un accidente. Fue una invitación.",
            3.5, 18.5, 0.0,
            false, 320, "laboratorio"),
        new LevelInfo(5, "ARSENAL ORBITAL", "Nivel -4 · Armería",
            new[] { "Si queda algo con lo que pelear de verdad, está en el arsenal.", "También está lo que lo custodia ahora: caballeros infernales, tres metros de rencor.", "Toma el fusil de plasma antes de que te tomen a ti." },
            "Estás armado. Por primera vez desde que aterrizaste, ellos deberían preocuparse.",
            3.5, 19.5, 0.0,
            false, 380, "arsenal"),
        new LevelInfo(6, "LA COLMENA", "Nivel -5 · Núcleo biológico",
            new[] { "La estación dejó de ser una estación. Las paredes respiran.", "Los mancubus guardan el nido; el depósito de cohetes está detrás de ellos.", "Abre las tres compuertas y sube al nido de la reina." },
            "El nido está muerto. Debajo late algo mucho más grande.",
            3.5, 21.5, 0.0,
            false, 460, "colmena"),
        new LevelInfo(7, "EL TRONO DEL CIBERDEMONIO", "Nivel -6 · Portal",
            new[] { "Aquí termina. El Ciberdemonio mantiene el portal abierto con su sola presencia.", "Consigue la tarjeta roja del ala oeste y entra en la arena.", "Mátalo y sube al balcón de evacuación. No hay plan B." },
            "El portal se cierra. El Sector 09 vuelve a ser solo metal y silencio.",
            15.5, 23.5, -1.5708,
            true, 420, "infierno"),
    };
}
