using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Presentation;

// Raycaster por software: genera píxeles, nunca aplica daño ni modifica entidades.
// No usa System.Drawing: también se puede verificar en una ejecución sin ventana.
public sealed class PixelRenderer
{
    public const int Width = 640, Height = 360;
    private readonly ShipVisuals ship = new();
    private readonly int[] skyColumns = new int[Width], skyRows = new int[Height];
    private int[]? sky;
    public int[] Pixels { get; } = new int[Width * Height];

    private MissionPlan? renderedPlan;
    private readonly HashSet<(int, int)> hazardCells = new();
    private readonly Dictionary<MissionKind, Sprite> missionSpritesOff = Enum.GetValues<MissionKind>().ToDictionary(k => k, k => BuildObjective(k, false));
    private readonly Dictionary<MissionKind, Sprite> missionSpritesOn = Enum.GetValues<MissionKind>().ToDictionary(k => k, k => BuildObjective(k, true));
    private readonly Sprite droneSprite = BuildMissionSprite(true, false);
    private static Sprite BuildMissionSprite(bool drone, bool activated)
    {
        var s = new Sprite(32, 40);
        int dark = Palette.Rgb(24, 41, 51), light = Palette.Rgb(91, 234, 218);
        if (drone)
        {
            s.Rect(2, 15, 28, 12, dark); s.Rect(9, 11, 14, 20, Palette.Rgb(155, 186, 197));
            s.Rect(12, 15, 8, 6, light); s.Rect(0, 9, 8, 4, dark); s.Rect(24, 9, 8, 4, dark);
            s.Rect(3, 31, 5, 5, light); s.Rect(24, 31, 5, 5, light);
        }
        else
        {
            // Panel de interruptor de pared: marco, placa metálica, palanca sobre
            // su pivote y una luz de estado. Al activarse la palanca baja hacia
            // su tope y la luz pasa de roja a verde, en vez de teñir el sprite
            // entero de un solo color plano.
            int panel = Palette.Rgb(58, 66, 68), plate = Palette.Rgb(35, 43, 45), bolt = Palette.Rgb(16, 19, 21);
            int handle = Palette.Rgb(198, 202, 206), knob = Palette.Rgb(230, 233, 236);
            int statusLight = activated ? Palette.Rgb(78, 224, 122) : Palette.Rgb(214, 60, 52);
            s.Rect(4, 2, 24, 37, dark);
            s.Rect(6, 4, 20, 33, panel);
            s.Rect(8, 6, 16, 26, plate);
            s.Rect(7, 5, 2, 2, bolt); s.Rect(23, 5, 2, 2, bolt);
            s.Rect(7, 35, 2, 2, bolt); s.Rect(23, 35, 2, 2, bolt);
            s.Ellipse(16, 15, 3, 3, statusLight);
            s.Ellipse(16, 27, 3, 3, bolt);
            if (activated)
            {
                // Palanca abajo, apoyada contra la placa.
                s.Line(16, 27, 11, 33, handle, 4);
                s.Ellipse(11, 33, 3, 3, knob);
            }
            else
            {
                // Palanca arriba, en reposo.
                s.Line(16, 27, 23, 10, handle, 4);
                s.Ellipse(23, 10, 3, 3, knob);
            }
        }
        return s;
    }
    private static Sprite BuildObjective(MissionKind kind, bool activated)
    {
        var s = BuildMissionSprite(false, activated);
        int cyan = Palette.Rgb(83, 239, 228), dark = Palette.Rgb(24, 45, 62), metal = Palette.Rgb(167, 190, 202);
        int accent = activated ? Palette.Rgb(78, 224, 122) : cyan;
        int indicator = activated ? Palette.Rgb(78, 224, 122) : Palette.Rgb(214, 60, 52);
        if (kind == MissionKind.Containment)
        {
            Array.Clear(s.Pixels); s.Rect(13, 3, 6, 37, metal);
            s.Ring(16, 18, 13, 13, Palette.Rgb(251, 124, 53));
            s.Line(6, 8, 25, 27, accent, 2); s.Line(25, 8, 6, 27, accent, 2); s.Ellipse(16, 18, 4, 4, metal);
            s.Ellipse(16, 4, 2, 2, indicator);
        }
        else if (kind == MissionKind.Demolition)
        {
            Array.Clear(s.Pixels); s.Rect(5, 14, 22, 22, Palette.Rgb(210, 157, 67));
            s.Rect(8, 17, 16, 9, dark); s.Rect(10, 19, 12, 4, accent);
            s.Line(10, 14, 5, 4, metal, 2); s.Line(23, 14, 27, 4, metal, 2);
            s.Ellipse(27, 16, 2, 2, indicator);
        }
        else if (kind == MissionKind.Uplink)
        {
            Array.Clear(s.Pixels); s.Rect(13, 14, 6, 25, dark); s.Rect(5, 35, 22, 5, metal);
            s.Ellipse(16, 13, 14, 9, metal); s.Ellipse(16, 12, 10, 6, dark);
            s.Line(16, 14, 23, 2, accent, 2);
            s.Ellipse(16, 37, 2, 2, indicator);
        }
        else if (kind == MissionKind.BossShield)
        {
            Array.Clear(s.Pixels); s.Rect(7, 3, 18, 37, dark); s.Rect(11, 5, 10, 29, accent);
            s.Rect(4, 33, 24, 7, metal); s.Rect(5, 3, 22, 3, metal);
            s.Ellipse(16, 6, 2, 2, indicator);
        }
        return s;
    }
    private readonly double[] depth = new double[Width];
    private readonly Dictionary<int, Sprite> enemySprites = new();
    private readonly Dictionary<PickupKind, Sprite> pickupSprites = new();
    private readonly Dictionary<ProjectileKind, Sprite> shotSprites = new();
    private readonly List<(int X0, int Y0, int X1, int Y1, int Tier)> cracks = new();

    private static readonly EnemyState[] States =
        { EnemyState.Walk, EnemyState.WindUp, EnemyState.Attack, EnemyState.Pain, EnemyState.Dead };

    public PixelRenderer()
    {
        foreach (EnemyKind kind in Enum.GetValues<EnemyKind>())
            foreach (var state in States)
            {
                int frames = state == EnemyState.Walk ? 2 : 1;
                for (int f = 0; f < frames; f++)
                    enemySprites[Key(kind, state, f)] = SpriteFactory.Enemy(kind, state, f);
            }
        foreach (PickupKind kind in Enum.GetValues<PickupKind>())
            pickupSprites[kind] = SpriteFactory.Pickup(kind);
        foreach (ProjectileKind kind in Enum.GetValues<ProjectileKind>())
            shotSprites[kind] = SpriteFactory.Projectile(kind);
        BuildCracks();
    }

    private static int Key(EnemyKind kind, EnemyState state, int frame)
        => (int)kind * 100 + (int)state * 10 + frame;

    // Grietas del cristal del visor, calculadas una vez con semilla fija.
    private void BuildCracks()
    {
        var random = new Random(77);
        (int x, int y)[] impacts = { (86, 68), (392, 88), (150, 214), (424, 206), (44, 168) };
        for (int i = 0; i < impacts.Length; i++)
        {
            var (ox, oy) = impacts[i];
            int tier = i < 1 ? 0 : i < 3 ? 1 : 2;
            int branches = 4 + random.Next(3);
            for (int b = 0; b < branches; b++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                int x = ox, y = oy;
                int segments = 3 + random.Next(3);
                for (int s = 0; s < segments; s++)
                {
                    angle += (random.NextDouble() - 0.5) * 1.1;
                    int length = 14 + random.Next(34);
                    int nx = x + (int)(Math.Cos(angle) * length);
                    int ny = y + (int)(Math.Sin(angle) * length);
                    cracks.Add((x, y, nx, ny, Math.Min(2, tier + (s > 2 ? 1 : 0))));
                    x = nx; y = ny;
                }
            }
        }
    }

    public void Render(GameService game)
    {
        var p = game.Player;
        if (!ReferenceEquals(renderedPlan, game.Mission?.Plan))
        {
            renderedPlan = game.Mission?.Plan; hazardCells.Clear();
            if (renderedPlan is not null) foreach (var h in renderedPlan.Hazards) hazardCells.Add(((int)h.X, (int)h.Y));
        }
        double shake = game.Shake;
        double angle = p.Angle + Math.Cos(game.ElapsedFrames * 0.9) * shake * 0.004;
        // El salto solo desplaza la cámara en un arco (sin obstáculos con
        // altura en este mundo), pero basta para que se sienta como un salto.
        int horizon = Height / 2 + (int)(Math.Tan(p.Pitch) * Height + Math.Sin(game.ElapsedFrames * 1.6) * shake * 1.6
            - p.JumpHeight * 220);
        double dirX = Math.Cos(angle), dirY = Math.Sin(angle);
        double planeX = -dirY * 0.66, planeY = dirX * 0.66;
        string theme = game.Level.Theme;
        sky = ShipVisuals.IsShip(theme) ? ship.Sky(theme) : null;
        if (sky is not null)
        {
            for (int x = 0; x < Width; x++)
            {
                double c = 2d * x / Width - 1;
                double a = Math.Atan2(dirY + planeY * c, dirX + planeX * c);
                skyColumns[x] = ((int)((a + Math.PI) / (2 * Math.PI) * ShipVisuals.SkyWidth)) % ShipVisuals.SkyWidth;
            }
            for (int y = 0; y < Height; y++)
                skyRows[y] = Math.Clamp((int)((0.5 - Math.Atan2(horizon - y, Height) / Math.PI) * ShipVisuals.SkyHeight), 0, ShipVisuals.SkyHeight - 1) * ShipVisuals.SkyWidth;
        }

        DrawFloorAndCeiling(game, horizon, dirX, dirY, planeX, planeY, theme);
        DrawWalls(game, horizon, dirX, dirY, planeX, planeY, theme);
        DrawBillboards(game, horizon, dirX, dirY);
        DrawWeapon(game);
        DrawOverlays(game);
    }

    // ----------------------------------------------------------------- suelos

    private void DrawFloorAndCeiling(GameService game, int horizon,
        double dirX, double dirY, double planeX, double planeY, string theme)
    {
        var p = game.Player;
        var (ceilA, ceilB, floorA, floorB) = ThemeGround(theme);
        for (int y = 0; y < Height; y++)
        {
            bool ceiling = y < horizon;
            double rowDistance = Height * (ceiling ? game.World.CeilingHeight - 0.5 : 0.5) / Math.Max(1, Math.Abs(y - horizon));
            double light = Math.Clamp(1 / (1 + rowDistance * 0.080), 0.22, 1);
            for (int x = 0; x < Width; x++)
            {
                double camera = 2d * x / Width - 1;
                double wx = p.X + rowDistance * (dirX + planeX * camera);
                double wy = p.Y + rowDistance * (dirY + planeY * camera);
                int tx = ((int)Math.Floor(wx * 32) % 32 + 32) % 32;
                int ty = ((int)Math.Floor(wy * 32) % 32 + 32) % 32;
                int color;
                int cellX = (int)Math.Floor(wx), cellY = (int)Math.Floor(wy);
                if (sky is not null && ceiling && (theme is "observatorio" or "puente" || theme == "hangar" && cellX is >= 12 and <= 19)
                    && tx > 1 && ty > 1)
                {
                    Pixels[y * Width + x] = Palette.Mix(sky[skyRows[y] + skyColumns[x]], Palette.Rgb(41, 100, 132), 0.06);
                    continue;
                }
                if (ceiling)
                    color = tx == 0 || ty == 0 ? Palette.Scale(ceilA, 0.55) : ceilA;
                else
                {
                    bool check = (((int)Math.Floor(wx) + (int)Math.Floor(wy)) & 1) == 0;
                    int noise = (tx * 13 + ty * 7) % 7;
                    color = tx < 1 || ty < 1 ? Palette.Scale(floorA, 0.45)
                        : check ? Shift(floorA, noise) : Shift(floorB, noise);
                    if (sky is not null) color = ShipVisuals.Floor(theme, tx, ty, cellX, cellY);
                    if (game.World.Tile(cellX, cellY) == 'X')
                        color = game.CanExit ? Palette.Rgb(40, 177, 97) : Palette.Rgb(141, 48, 39);
                    if (game.Mission is { } mission)
                    {
                        if (hazardCells.Contains(((int)Math.Floor(wx), (int)Math.Floor(wy))))
                            color = ((tx + ty) / 5) % 2 == 0 ? Palette.Rgb(27, 38, 45)
                                : mission.ElectricityOn ? Palette.Rgb(240, 80, 32) : Palette.Rgb(31, 151, 204);
                        if (mission.Plan.Kind == MissionKind.Uplink)
                        {
                            var n = mission.Plan.Points[0];
                            double d = Math.Sqrt((wx - n.X) * (wx - n.X) + (wy - n.Y) * (wy - n.Y));
                            if (Math.Abs(d - 2.2) < 0.10) color = Palette.Rgb(51, 241, 217);
                        }
                    }
                }
                if (ceiling && (tx < 2 || ty < 2) && (tx + ty) % 8 < 2) color = ceilB;
                if (sky is not null && ceiling)
                {
                    color = tx < 3 || ty < 3 ? Palette.Rgb(45, 64, 78) : Palette.Rgb(21, 33, 45);
                    if (tx > 10 && tx < 23 && ty > 12 && ty < 19)
                        color = unchecked((int)0xFF000000) | ShipVisuals.Accent(theme);
                }
                if (sky is not null && !ceiling && tx > 28 && cellX % 4 == 0)
                    color = Palette.Mix(color, unchecked((int)0xFF000000) | ShipVisuals.Accent(theme), 0.55);
                Pixels[y * Width + x] = Palette.Scale(color, light);
            }
        }
    }

    private static int Shift(int color, int amount) => Palette.Rgb(
        ((color >> 16) & 255) + amount, ((color >> 8) & 255) + amount, (color & 255) + amount);

    private static (int Ceiling, int CeilingSpot, int FloorA, int FloorB) ThemeGround(string theme) => theme switch
    {
        "servicio" => (Palette.Rgb(28, 36, 39), Palette.Rgb(48, 62, 66),
                       Palette.Rgb(52, 60, 62), Palette.Rgb(43, 50, 52)),
        "carga" => (Palette.Rgb(36, 31, 26), Palette.Rgb(64, 50, 36),
                    Palette.Rgb(66, 56, 44), Palette.Rgb(54, 46, 37)),
        "laboratorio" => (Palette.Rgb(38, 44, 44), Palette.Rgb(70, 86, 84),
                          Palette.Rgb(74, 80, 76), Palette.Rgb(60, 68, 66)),
        "arsenal" => (Palette.Rgb(26, 30, 38), Palette.Rgb(48, 56, 72),
                      Palette.Rgb(56, 60, 70), Palette.Rgb(45, 49, 59)),
        "colmena" => (Palette.Rgb(38, 24, 24), Palette.Rgb(72, 36, 34),
                      Palette.Rgb(74, 44, 40), Palette.Rgb(60, 35, 33)),
        "infierno" => (Palette.Rgb(44, 18, 16), Palette.Rgb(96, 34, 22),
                       Palette.Rgb(78, 36, 28), Palette.Rgb(58, 26, 22)),
        _ => (Palette.Rgb(33, 39, 41), Palette.Rgb(52, 60, 62),
              Palette.Rgb(58, 57, 48), Palette.Rgb(48, 48, 43))
    };

    // ------------------------------------------------------------------ muros

    private void DrawWalls(GameService game, int horizon,
        double dirX, double dirY, double planeX, double planeY, string theme)
    {
        var p = game.Player;
        for (int x = 0; x < Width; x++)
        {
            double camera = 2d * x / Width - 1;
            var hit = game.World.CastRay(p.X, p.Y, dirX + planeX * camera, dirY + planeY * camera);
            depth[x] = hit.Distance;
            int wallHeight = Math.Min(Height * 100, (int)(Height * game.World.CeilingHeight / Math.Max(0.02, hit.Distance)));
            int bottom = horizon + (int)(Height * 0.5 / Math.Max(0.02, hit.Distance));
            int top = bottom - wallHeight;
            double u = hit.Side == 0 ? hit.Y : hit.X;
            u -= Math.Floor(u);
            int tx = Math.Clamp((int)(u * 64), 0, 63);
            double light = Math.Clamp(1 / (1 + hit.Distance * 0.085), 0.30, 1) * (hit.Side == 1 ? 0.84 : 1);
            var texture = sky is null ? null : ship.Wall(theme, hit.Material, hit.CellX + hit.CellY);
            for (int y = Math.Max(0, top); y < Math.Min(Height, bottom); y++)
            {
                int ty = Math.Clamp((y - top) * 64 / Math.Max(1, wallHeight), 0, 63);
                int color;
                if (sky is not null && hit.Material == 'W')
                {
                    // Marco y cristal: colisión de casco normal, panorama a distancia infinita.
                    bool frame = tx < 3 || tx > 60 || ty < 7 || ty > 56 || ty is 31 or 32;
                    color = frame ? Palette.Rgb(68, 94, 110)
                        : Palette.Mix(sky[skyRows[y] + skyColumns[x]], Palette.Rgb(52, 109, 140), 0.07);
                    if (frame && ty is 7 or 56) color = Palette.Rgb(135, 213, 236);
                    Pixels[y * Width + x] = frame ? Palette.Scale(color, light) : color;
                }
                else
                {
                    color = texture is null ? WallTexel(tx, ty, hit.Material, hit.CellX, theme) : texture[ty * 64 + tx];
                    // Las bandas luminosas conservan su emisión, incluso al fondo de la sala.
                    double illumination = texture is not null && ty is 6 or 57 ? Math.Max(0.75, light) : light;
                    Pixels[y * Width + x] = Palette.Scale(color, illumination);
                }
            }
        }
    }

    // Textura procedimental: el material manda y el tema pinta.
    private static int WallTexel(int x, int y, char material, int sector, string theme)
    {
        int noise = (x * 37 + y * 19) % 11;
        if (material is 'R' or 'B' or 'Y')
        {
            int panel = material == 'R' ? Palette.Rgb(132, 38, 30)
                : material == 'B' ? Palette.Rgb(30, 68, 138) : Palette.Rgb(150, 126, 32);
            if (x < 4 || x > 59) return Palette.Rgb(88, 95, 96);
            if (y < 6 || y > 57) return ((x + y) / 6) % 2 == 0 ? Palette.Rgb(177, 139, 43) : Palette.Rgb(27, 30, 29);
            if (x is 31 or 32) return Palette.Rgb(13, 17, 21);
            if (y % 12 == 0) return Palette.Rgb(30, 35, 36);
            return Shift(panel, noise);
        }
        var (baseColor, dark, accent) = ThemeWall(theme);
        if (material == '=')
        {
            if (y < 3 || y > 60 || x < 3 || x > 60) return Palette.Scale(dark, 0.9);
            if ((x is 8 or 55) && (y is 8 or 55)) return Palette.Rgb(198, 200, 196);
            if (y is 31 or 32) return dark;
            return Shift(Palette.Mix(baseColor, Palette.Rgb(150, 156, 160), 0.45), noise);
        }
        if (material == '%')
        {
            int vein = (x * 5 + y * 3) % 23;
            if (vein < 2) return Palette.Rgb(178, 58, 26);
            if (vein < 4) return Palette.Rgb(96, 26, 18);
            return Shift(Palette.Mix(baseColor, Palette.Rgb(104, 44, 34), 0.6), noise);
        }
        if (material == '&')
        {
            if (x < 6 || x > 57) return Palette.Scale(dark, 0.75);
            if (x % 9 < 2) return Palette.Scale(dark, 1.15);
            if (y < 5 || y > 58) return accent;
            return Shift(Palette.Mix(baseColor, accent, 0.35), noise);
        }
        if (y < 4 || y > 59) return dark;
        if (y == 9 && x > 6 && x < 57) return accent;
        if (x < 2 || x > 61 || y is 32 or 33) return Palette.Scale(dark, 1.08);
        if ((x is 6 or 57) && (y is 15 or 54)) return Palette.Rgb(155, 160, 150);
        if (y > 39 && y < 53 && x > 13 && x < 51 && y % 3 == 0) return Palette.Scale(dark, 1.12);
        int tint = sector % 3;
        return Shift(tint == 0 ? baseColor : tint == 1 ? Palette.Scale(baseColor, 0.92) : Palette.Scale(baseColor, 1.07), noise);
    }

    private static (int Base, int Dark, int Accent) ThemeWall(string theme) => theme switch
    {
        "servicio" => (Palette.Rgb(86, 104, 103), Palette.Rgb(33, 43, 45), Palette.Rgb(62, 132, 142)),
        "carga" => (Palette.Rgb(120, 86, 54), Palette.Rgb(44, 33, 26), Palette.Rgb(176, 102, 46)),
        "laboratorio" => (Palette.Rgb(122, 130, 122), Palette.Rgb(38, 46, 46), Palette.Rgb(96, 178, 140)),
        "arsenal" => (Palette.Rgb(102, 110, 128), Palette.Rgb(38, 44, 56), Palette.Rgb(126, 148, 190)),
        "colmena" => (Palette.Rgb(128, 72, 62), Palette.Rgb(46, 27, 26), Palette.Rgb(178, 82, 66)),
        "infierno" => (Palette.Rgb(114, 54, 40), Palette.Rgb(42, 18, 16), Palette.Rgb(206, 84, 34)),
        _ => (Palette.Rgb(91, 87, 68), Palette.Rgb(29, 34, 35), Palette.Rgb(156, 91, 49))
    };

    // ------------------------------------------------------------- billboards

    // Un billboard por dibujar. Se guardan como estructuras y la lista se
    // reutiliza entre fotogramas: dibujar 400 partículas no debe generar basura.
    private readonly struct Billboard
    {
        public readonly double Distance;
        public readonly int Source;   // 0 enemigo · 1 objeto · 2 proyectil · 3 partícula
        public readonly int Index;
        public Billboard(double distance, int source, int index)
        { Distance = distance; Source = source; Index = index; }
    }

    private readonly List<Billboard> billboards = new();
    private static readonly Comparison<Billboard> FarthestFirst =
        (a, b) => b.Distance.CompareTo(a.Distance);

    private void DrawBillboards(GameService game, int horizon, double dirX, double dirY)
    {
        var p = game.Player;
        billboards.Clear();

        double Squared(double x, double y) => (x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y);

        var enemies = game.World.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            billboards.Add(new Billboard(Squared(enemies[i].X, enemies[i].Y), 0, i));
        var pickups = game.World.Pickups;
        for (int i = 0; i < pickups.Count; i++)
            if (!pickups[i].Collected)
                billboards.Add(new Billboard(Squared(pickups[i].X, pickups[i].Y), 1, i));
        var shots = game.Projectiles;
        for (int i = 0; i < shots.Count; i++)
            billboards.Add(new Billboard(Squared(shots[i].X, shots[i].Y), 2, i));
        var particles = game.Particles;
        for (int i = 0; i < particles.Count; i++)
            billboards.Add(new Billboard(Squared(particles[i].X, particles[i].Y), 3, i));

        if (game.Mission is { } mission)
        {
            for (int i = 0; i < mission.Nodes.Count; i++)
            {
                var pt = mission.Nodes[i].Point;
                double xx = mission.Plan.Kind == MissionKind.Escort ? mission.DroneX : pt.X;
                double yy = mission.Plan.Kind == MissionKind.Escort ? mission.DroneY : pt.Y;
                billboards.Add(new Billboard(Squared(xx, yy), 4, i));
            }
        }
        billboards.Sort(FarthestFirst);

        foreach (var entry in billboards)
        {
            switch (entry.Source)
            {
                case 0:
                    DrawEnemy(game, enemies[entry.Index], horizon, dirX, dirY);
                    break;
                case 1:
                    var item = pickups[entry.Index];
                    double bob = Math.Sin(game.ElapsedFrames * 0.06 + item.X + item.Y) * 0.05;
                    Blit(pickupSprites[item.Kind], item.X, item.Y, 0.15 + bob, 0.38,
                        horizon, dirX, dirY, p.X, p.Y, 1, 0, false);
                    break;
                case 2:
                    var shot = shots[entry.Index];
                    Blit(shotSprites[shot.Kind], shot.X, shot.Y, shot.Height - 0.15, 0.30,
                        horizon, dirX, dirY, p.X, p.Y, 1, 0, true);
                    break;
                case 4:
                    var state = game.Mission!;
                    var node = state.Nodes[entry.Index];
                    bool isDrone = state.Plan.Kind == MissionKind.Escort;
                    var missionSprite = isDrone ? droneSprite
                        : (node.Activated ? missionSpritesOn : missionSpritesOff)[state.Plan.Kind];
                    Blit(missionSprite,
                        isDrone ? state.DroneX : node.Point.X, isDrone ? state.DroneY : node.Point.Y,
                        isDrone ? 0.25 : 0, isDrone ? 0.40 : 0.62,
                        horizon, dirX, dirY, p.X, p.Y, 1, 0, true);
                    break;
                default:
                    DrawParticle(particles[entry.Index], horizon, dirX, dirY, p.X, p.Y);
                    break;
            }
        }
    }

    private void DrawEnemy(GameService game, Enemy enemy, int horizon, double dirX, double dirY)
    {
        var p = game.Player;
        var state = !enemy.IsAlive ? EnemyState.Dead
            : enemy.HitFlashFrames > 5 ? EnemyState.Pain
            : enemy.State == EnemyState.Idle ? EnemyState.Walk : enemy.State;
        if (state == EnemyState.Pain && enemy.State == EnemyState.WindUp) state = EnemyState.WindUp;
        int period = AnimationPeriod(enemy.Kind);
        int frame = state == EnemyState.Walk ? enemy.AnimTimer / period % 2 : 0;
        var sprite = enemySprites[Key(enemy.Kind, state, frame)];

        double height = enemy.Bob + (enemy.Flying ? 0.42 : 0);
        double scale = enemy.SpriteScale;
        if (!enemy.IsAlive)
        {
            height = 0;
            scale = enemy.SpriteScale * Math.Max(0.34, 1 - enemy.DeathTimer / 22.0);
        }
        double alpha = enemy.Alpha;
        if (enemy.Kind == EnemyKind.Espectro && enemy.IsAlive)
            alpha = 0.30 + Math.Sin(enemy.AnimTimer * 0.08) * 0.12;
        Blit(sprite, enemy.X, enemy.Y, height, scale, horizon, dirX, dirY, p.X, p.Y,
            alpha, enemy.HitFlashFrames > 5 ? Palette.Rgb(255, 236, 190) : 0, false);
    }

    private static int AnimationPeriod(EnemyKind kind) => kind switch
    {
        EnemyKind.AlmaPerdida => 4,
        EnemyKind.Espectro => 5,
        EnemyKind.Demonio => 7,
        EnemyKind.Imp => 9,
        EnemyKind.CaballeroInfernal => 10,
        EnemyKind.Zombi => 12,
        EnemyKind.Cacodemon => 14,
        EnemyKind.Mancubus => 16,
        _ => 18
    };

    // Proyección de un sprite del mundo a la pantalla, con prueba de profundidad.
    private void Blit(Sprite sprite, double worldX, double worldY, double height, double scale,
        int horizon, double dirX, double dirY, double px, double py,
        double alpha, int tint, bool glow)
    {
        double sx = worldX - px, sy = worldY - py;
        double forward = sx * dirX + sy * dirY;
        // Muy cerca el sprite taparía la pantalla entera: se omite.
        if (forward <= 0.30) return;
        double side = (-sx * dirY + sy * dirX) / 0.66;
        double vertical = Height / forward;
        int center = (int)(Width * 0.5 * (1 + side / forward));
        int h = Math.Max(1, (int)(vertical * scale));
        int w = Math.Max(1, (int)(vertical * scale * sprite.Width / (double)sprite.Height));
        int bottom = (int)(horizon + vertical / 2 - height * vertical);
        int top = bottom - h, left = center - w / 2;
        double light = glow ? 1 : Math.Clamp(1 / (1 + forward * 0.07), 0.32, 1);

        for (int xx = Math.Max(0, left); xx < Math.Min(Width, left + w); xx++)
        {
            if (forward >= depth[xx]) continue;
            int tx = Math.Clamp((xx - left) * sprite.Width / w, 0, sprite.Width - 1);
            for (int yy = Math.Max(0, top); yy < Math.Min(Height, bottom); yy++)
            {
                int ty = Math.Clamp((yy - top) * sprite.Height / h, 0, sprite.Height - 1);
                int color = sprite.Pixels[ty * sprite.Width + tx];
                if (color == 0) continue;
                if (tint != 0) color = tint;
                color = Palette.Scale(color, light);
                int index = yy * Width + xx;
                Pixels[index] = alpha >= 0.999 ? color : Palette.Mix(Pixels[index], color, alpha);
            }
        }
    }

    private void DrawParticle(Particle particle, int horizon, double dirX, double dirY, double px, double py)
    {
        double sx = particle.X - px, sy = particle.Y - py;
        double forward = sx * dirX + sy * dirY;
        if (forward <= 0.12) return;
        double side = (-sx * dirY + sy * dirX) / 0.66;
        double vertical = Height / forward;
        int center = (int)(Width * 0.5 * (1 + side / forward));
        int size = Math.Max(1, (int)(vertical * particle.Size));
        int cy = (int)(horizon + vertical / 2 - particle.Height * vertical);
        double fade = particle.Fade;
        int color = particle.Kind switch
        {
            ParticleKind.Smoke => Palette.Mix(particle.Color, Palette.Rgb(20, 20, 22), 1 - fade),
            ParticleKind.Fire => Palette.Mix(Palette.Rgb(255, 236, 170), particle.Color, 1 - fade),
            _ => particle.Color
        };
        double light = Math.Clamp(1 / (1 + forward * 0.07), 0.35, 1);
        color = Palette.Scale(color, light);
        for (int yy = cy - size / 2; yy < cy + size / 2 + 1; yy++)
        {
            if (yy < 0 || yy >= Height) continue;
            for (int xx = center - size / 2; xx < center + size / 2 + 1; xx++)
            {
                if (xx < 0 || xx >= Width || forward >= depth[xx]) continue;
                int index = yy * Width + xx;
                Pixels[index] = Palette.Mix(Pixels[index], color, Math.Clamp(fade + 0.25, 0, 1));
            }
        }
    }

    // ------------------------------------------------------------------ armas

    private void DrawWeapon(GameService game)
    {
        var weapon = game.Player.CurrentWeapon;
        int bob = (int)(Math.Sin(game.ElapsedFrames * 0.12) * 2);
        int recoil = (int)(game.Player.ViewKick * 5) + game.ShotFlashFrames * 2;
        int hide = game.WeaponSwitchFrames * 6;
        int cx = 240 + bob, y = 270 - 74 + recoil + hide - (int)(game.Player.JumpHeight * 150);
        bool flash = game.ShotFlashFrames >= 3;

        void Rect(int x, int yy, int w, int h, int color)
        {
            int left = x * Width / 480, top = yy * Height / 270;
            int right = (x + w) * Width / 480, bottom = (yy + h) * Height / 270;
            for (int j = Math.Max(0, top); j < Math.Min(Height, bottom); j++)
                for (int i = Math.Max(0, left); i < Math.Min(Width, right); i++) Pixels[j * Width + i] = color;
        }
        void Hands(int offset)
        {
            Rect(cx - 46 + offset, y + 44, 26, 46, Palette.Rgb(29, 49, 67));
            Rect(cx + 20 - offset, y + 40, 26, 46, Palette.Rgb(49, 76, 96));
            Rect(cx - 38 + offset, y + 39, 18, 20, Palette.Rgb(99, 125, 138));
        }

        switch (weapon.Kind)
        {
            case WeaponKind.Pistola:
                Hands(10);
                Rect(cx - 9, y + 26, 18, 34, Palette.Rgb(38, 42, 45));
                Rect(cx - 7, y + 12, 13, 20, Palette.Rgb(64, 70, 74));
                Rect(cx - 5, y + 6, 9, 10, Palette.Rgb(96, 104, 108));
                Rect(cx - 2, y + 3, 3, 5, Palette.Rgb(190, 186, 150));
                if (flash)
                {
                    Rect(cx - 5, y - 12, 10, 16, Palette.Rgb(255, 226, 120));
                    Rect(cx - 12, y - 7, 24, 6, Palette.Rgb(255, 172, 54));
                }
                break;
            case WeaponKind.EscopetaDoble:
                Hands(2);
                Rect(cx - 30, y + 20, 60, 22, Palette.Rgb(28, 31, 33));
                Rect(cx - 26, y + 8, 24, 30, Palette.Rgb(78, 84, 84));
                Rect(cx + 2, y + 8, 24, 30, Palette.Rgb(66, 72, 73));
                Rect(cx - 22, y + 2, 16, 12, Palette.Rgb(120, 128, 128));
                Rect(cx + 6, y + 2, 16, 12, Palette.Rgb(104, 112, 112));
                Rect(cx - 16, y + 42, 32, 16, Palette.Rgb(104, 72, 40));
                if (flash)
                {
                    Rect(cx - 24, y - 22, 18, 28, Palette.Rgb(255, 232, 140));
                    Rect(cx + 6, y - 22, 18, 28, Palette.Rgb(255, 232, 140));
                    Rect(cx - 34, y - 14, 68, 10, Palette.Rgb(255, 158, 43));
                }
                break;
            case WeaponKind.Ametralladora:
                Hands(6);
                Rect(cx - 20, y + 24, 40, 26, Palette.Rgb(42, 46, 48));
                Rect(cx - 8, y + 2, 16, 30, Palette.Rgb(72, 78, 80));
                Rect(cx - 5, y - 8, 10, 14, Palette.Rgb(120, 126, 128));
                Rect(cx - 24, y + 34, 14, 22, Palette.Rgb(58, 62, 64));
                for (int i = 0; i < 5; i++) Rect(cx - 4, y - 6 + i * 3, 8, 1, Palette.Rgb(36, 40, 42));
                if (flash)
                {
                    Rect(cx - 7, y - 24, 14, 20, Palette.Rgb(255, 236, 150));
                    Rect(cx - 16, y - 18, 32, 8, Palette.Rgb(255, 168, 50));
                }
                break;
            case WeaponKind.Plasma:
                Hands(4);
                Rect(cx - 24, y + 22, 48, 28, Palette.Rgb(44, 54, 64));
                Rect(cx - 14, y + 4, 28, 24, Palette.Rgb(60, 74, 88));
                Rect(cx - 9, y + 8, 18, 12, Palette.Rgb(72, 198, 244));
                Rect(cx - 6, y - 8, 12, 16, Palette.Rgb(90, 106, 120));
                Rect(cx - 3, y - 12, 6, 8, Palette.Rgb(206, 246, 255));
                if (flash)
                {
                    Rect(cx - 9, y - 26, 18, 18, Palette.Rgb(214, 246, 255));
                    Rect(cx - 18, y - 20, 36, 8, Palette.Rgb(96, 202, 255));
                }
                break;
            case WeaponKind.Lanzacohetes:
                Hands(0);
                Rect(cx - 30, y + 18, 60, 30, Palette.Rgb(64, 68, 70));
                Rect(cx - 30, y + 18, 60, 6, Palette.Rgb(92, 96, 98));
                Rect(cx - 12, y - 4, 24, 26, Palette.Rgb(48, 52, 54));
                Rect(cx - 8, y - 10, 16, 10, Palette.Rgb(120, 124, 126));
                Rect(cx + 18, y + 30, 16, 22, Palette.Rgb(40, 44, 46));
                if (flash)
                {
                    Rect(cx - 12, y - 30, 24, 24, Palette.Rgb(255, 220, 140));
                    Rect(cx - 26, y - 22, 52, 12, Palette.Rgb(255, 140, 40));
                }
                break;
            default: // escopeta de combate
                Hands(0);
                Rect(cx - 45, y + 40, 28, 45, Palette.Rgb(29, 49, 67));
                Rect(cx + 19, y + 35, 27, 45, Palette.Rgb(49, 76, 96));
                Rect(cx - 37, y + 35, 19, 22, Palette.Rgb(99, 125, 138));
                Rect(cx - 22, y + 20, 44, 62, Palette.Rgb(25, 28, 29));
                Rect(cx - 18, y + 13, 36, 51, Palette.Rgb(76, 82, 82));
                Rect(cx - 13, y, 26, 46, Palette.Rgb(37, 42, 45));
                Rect(cx - 10, y + 2, 7, 39, Palette.Rgb(115, 125, 125));
                Rect(cx + 1, y + 2, 7, 39, Palette.Rgb(69, 77, 78));
                Rect(cx - 10, y + 43, 20, 14, Palette.Rgb(99, 69, 39));
                for (int i = 0; i < 4; i++) Rect(cx - 11, y + 43 + i * 4, 22, 1, Palette.Rgb(42, 33, 27));
                Rect(cx - 2, y - 4, 4, 7, Palette.Rgb(199, 191, 149));
                if (flash)
                {
                    Rect(cx - 6, y - 25, 12, 26, Palette.Rgb(255, 220, 95));
                    Rect(cx - 18, y - 18, 36, 9, Palette.Rgb(255, 158, 43));
                    Rect(cx - 4, y - 19, 8, 18, Palette.Rgb(255, 255, 204));
                }
                break;
        }
        Rect(cx + 8, y + 34, 7, 3, Palette.Rgb(89, 223, 236));
        Rect(cx - 5, y + 30, 2, 20, Palette.Rgb(134, 151, 157));
    }

    // -------------------------------------------------------------- pantalla

    private void DrawOverlays(GameService game)
    {
        int health = game.Player.Health;

        if (game.DamageFlashFrames > 0)
        {
            double strength = game.DamageFlashFrames / 14.0;
            for (int i = 0; i < Pixels.Length; i++)
                Pixels[i] = Palette.Mix(Pixels[i], Palette.Rgb(190, 26, 22), 0.30 * strength);
        }
        if (game.HealFlashFrames > 0)
        {
            double strength = game.HealFlashFrames / 16.0;
            for (int i = 0; i < Pixels.Length; i++)
                Pixels[i] = Palette.Mix(Pixels[i], Palette.Rgb(120, 230, 150), 0.22 * strength);
        }

        // Al borde de la muerte el visor se raja y la sangre invade los bordes.
        if (health > 40 || health <= 0) return;
        double danger = Math.Clamp((40 - health) / 40.0, 0, 1);
        double pulse = 0.72 + 0.28 * Math.Sin(game.ElapsedFrames * (0.10 + danger * 0.16));
        double intensity = danger * pulse;

        // viñeta roja
        int cx = Width / 2, cy = Height / 2;
        double maxDistance = Math.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < Height; y++)
        {
            int dy = y - cy;
            for (int x = 0; x < Width; x++)
            {
                int dx = x - cx;
                double d = Math.Sqrt(dx * dx + dy * dy) / maxDistance;
                if (d < 0.52) continue;
                double t = (d - 0.52) / 0.48;
                Pixels[y * Width + x] = Palette.Mix(Pixels[y * Width + x],
                    Palette.Rgb(112, 12, 10), t * t * 0.72 * intensity);
            }
        }

        int tiers = danger > 0.80 ? 2 : danger > 0.52 ? 1 : 0;
        int glass = Palette.Rgb(232, 238, 244);
        int crackDark = Palette.Rgb(18, 16, 20);
        foreach (var crack in cracks)
        {
            if (crack.Tier > tiers) continue;
            DrawCrack(crack.X0, crack.Y0, crack.X1, crack.Y1, crackDark, glass, intensity);
        }
    }

    private void DrawCrack(int x0, int y0, int x1, int y1, int dark, int light, double intensity)
    {
        x0 = x0 * Width / 480; x1 = x1 * Width / 480;
        y0 = y0 * Height / 270; y1 = y1 * Height / 270;
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (int s = 0; s <= steps; s++)
        {
            double t = steps == 0 ? 0 : s / (double)steps;
            int x = (int)(x0 + (x1 - x0) * t), y = (int)(y0 + (y1 - y0) * t);
            Plot(x, y, dark, 0.80 * intensity);
            Plot(x + 1, y, light, 0.42 * intensity);
            Plot(x, y + 1, light, 0.24 * intensity);
            Plot(x - 1, y, dark, 0.22 * intensity);
        }
    }

    private void Plot(int x, int y, int color, double alpha)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int index = y * Width + x;
        Pixels[index] = Palette.Mix(Pixels[index], color, Math.Clamp(alpha, 0, 1));
    }
}
