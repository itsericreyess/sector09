namespace DoomFPS.Presentation;

// Materiales y panorama originales generados una vez. Sin recursos externos ni descarga en juego.
internal sealed class ShipVisuals
{
    public const int SkyWidth = 1536, SkyHeight = 768;
    private readonly Dictionary<string, int[]> skies = new();
    private readonly Dictionary<(string, char, int), int[]> walls = new();
    internal static readonly string[] Themes = { "hangar", "energia", "bodega", "refrigeracion", "observatorio", "reactor", "puente" };
    public static bool IsShip(string theme) => Array.IndexOf(Themes, theme) >= 0;
    public static int Accent(string theme) => theme switch
    {
        "energia" => 0xF6BA4C, "bodega" => 0xEA843D, "refrigeracion" => 0x5CFFD0,
        "observatorio" => 0xB699FF, "reactor" => 0xFF5D38, "puente" => 0x72D9FC, _ => 0x8FDDFF
    };
    private static int Rgb(int c) => unchecked((int)0xFF000000) | c;
    public int[] Sky(string theme)
    {
        if (skies.TryGetValue(theme, out var pixels)) return pixels;
        pixels = new int[SkyWidth * SkyHeight];
        int level = Math.Max(0, Array.IndexOf(Themes, theme));
        double radius = 100 + level * 14, cx = SkyWidth * 0.24, cy = SkyHeight * 0.47;
        for (int y = 0; y < SkyHeight; y++) for (int x = 0; x < SkyWidth; x++)
        {
            double band = Math.Exp(-Math.Pow((y - 310 - 90 * Math.Sin(x * 0.008)) / 90, 2));
            double mist = (Math.Sin(x * 0.019 + y * 0.011) + Math.Cos(y * 0.025 - x * 0.01) + 2) / 4;
            int col = Palette.Rgb(3 + (int)(12 * band * mist), 6 + (int)(15 * band), 16 + (int)(30 * band * mist));
            double dx = (x - cx) / radius, dy = (y - cy) / radius, d2 = dx * dx + dy * dy;
            if (d2 < 1)
            {
                double z = Math.Sqrt(1 - d2);
                double day = Math.Clamp((-dx * 0.6 - dy * 0.35 + z * 0.65), 0.035, 1);
                double continents = Noise(dx * 3 + 9, dy * 3 + 7) + Noise(dx * 8 + 2, dy * 8 + 5) * 0.3;
                col = continents > 0.60 ? Rgb(0x467964) : Rgb(0x1C63AD);
                double cloud = Noise(dx * 14 + 10 + Math.Sin(dy * 6), dy * 22 + 30) + Noise(dx * 31, dy * 37) * 0.25;
                if (cloud > 0.59) col = Palette.Mix(col, Rgb(0xE4F4FF), Math.Clamp((cloud - 0.59) * 3.5, 0, 0.85));
                col = Palette.Scale(col, day);
                col = Palette.Mix(col, Rgb(0x529EDD), Math.Pow(1 - z, 4) * 0.7);
            }
            else if (d2 < 1.14) col = Palette.Mix(col, Rgb(0x4AACFF), Math.Pow((1.14 - d2) / 0.14, 2) * 0.8);
            pixels[y * SkyWidth + x] = col;
        }
        var random = new Random(909);
        for (int n = 0; n < 4200; n++)
        {
            int x = random.Next(SkyWidth), y = random.Next(SkyHeight);
            if (Math.Pow((x - cx) / radius, 2) + Math.Pow((y - cy) / radius, 2) < 1.15) continue;
            int v = random.Next(85, 245); pixels[y * SkyWidth + x] = Palette.Rgb(v, v, Math.Min(255, v + 20));
            if (n % 120 == 0 && x > 0 && x < SkyWidth - 1 && y > 0 && y < SkyHeight - 1)
            { int c = Rgb(0x486E90); pixels[y * SkyWidth + x - 1] = c; pixels[y * SkyWidth + x + 1] = c; pixels[(y - 1) * SkyWidth + x] = c; pixels[(y + 1) * SkyWidth + x] = c; }
        }
        skies[theme] = pixels; return pixels;
    }
    private static double Noise(double x, double y)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        static double Hash(int xx, int yy)
        { uint h = unchecked((uint)(xx * 374761393 + yy * 668265263)); h = (h ^ (h >> 13)) * 1274126177; return (h & 65535) / 65535d; }
        double a = Hash(ix, iy) * (1 - fx) + Hash(ix + 1, iy) * fx;
        double b = Hash(ix, iy + 1) * (1 - fx) + Hash(ix + 1, iy + 1) * fx;
        return a * (1 - fy) + b * fy;
    }
    public int[] Wall(string theme, char material, int variant)
    {
        var key = (theme, material, variant % 4);
        if (walls.TryGetValue(key, out var texture)) return texture;
        texture = new int[64 * 64];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) texture[y * 64 + x] = Texel(theme, material, x, y, variant % 4);
        walls[key] = texture; return texture;
    }
    private static int Texel(string theme, char material, int x, int y, int variant)
    {
        int accent = Rgb(Accent(theme));
        int noise = (x * 7 + y * 13) % 5;
        int metal = theme switch { "refrigeracion" => 0xB0C7C4, "puente" => 0x496373,
            "bodega" => 0x796452, "observatorio" => 0x444362, "reactor" => 0x403B3D, _ => 0x647585 };
        int col = Palette.Scale(Rgb(metal), 0.9 + noise * 0.015);
        if (x < 2 || x > 61 || y < 2 || y > 61) return Rgb(0x14202A);
        if (x is 3 or 60 || y is 3 or 60) return Rgb(0x7C919F);
        if (y is 6 or 57) return accent;
        if (theme == "bodega" || (theme == "hangar" && material == '='))
        {
            if (x % 12 < 3) return Rgb(0x343D45);
            if (y > 43 && y < 53) return (x / 5 + y / 5) % 2 == 0 ? Rgb(0xECAF46) : Rgb(0x17212B);
            if (x > 18 && x < 46 && y > 15 && y < 33)
                return y % 4 == 0 || x % 7 == 0 ? Rgb(0xE9E1C7) : Rgb(0x253943);
            return col;
        }
        if (theme == "energia" || theme == "reactor")
        {
            if (x > 10 && x < 54 && y > 13 && y < 52)
            {
                if (x % 14 < 6) return Palette.Scale(accent, 0.45 + 0.5 * Math.Sin(Math.PI * (x % 14) / 6));
                if (y % 8 < 2) return Rgb(0x8B9BA5);
                return Rgb(0x12202B);
            }
            if ((x < 8 || x > 55) && y % 12 < 3) return Rgb(0xCCD9D7);
        }
        else if (theme == "refrigeracion" && material == '%')
        {
            if (x > 9 && x < 55 && y > 10 && y < 54)
            {
                double cylinder = Math.Sin((x - 9) * Math.PI / 46);
                if ((x * 13 + y * 17) % 71 == 0) return Rgb(0xA5FFE2);
                return Palette.Scale(Rgb(0x36BFB0), 0.2 + cylinder * 0.6);
            }
        }
        else if (theme is "puente" or "observatorio" || variant == 1)
        {
            if (x > 8 && x < 56 && y > 12 && y < 45)
            {
                if (x is 10 or 53 || y is 14 or 42) return accent;
                if (y % 7 < 2 && x > 15 && x < 22 + y / 2) return Palette.Scale(accent, 0.8);
                if (x > 39 && y < 34 && x % 4 == 0) return Rgb(0xD3EEE9);
                return Rgb(0x0A202C);
            }
        }
        if (y is 31 or 32) return Rgb(0x1B303E);
        if (x is 7 or 56 && y is 10 or 54) return Rgb(0xD6DEE0);
        if (variant == 3 && x > 22 && x < 42 && y > 18 && y < 50 && y % 4 == 0) return Rgb(0x263744);
        return col;
    }
    public static int Floor(string theme, int tx, int ty, int cx, int cy)
    {
        int seam = Rgb(0x152431), accent = Rgb(Accent(theme));
        if (tx == 0 || ty == 0) return seam;
        switch (theme)
        {
            case "hangar":
                if ((cx is 12 or 19) && tx < 4) return Rgb(0xD4B978);
                if (cx is 15 or 16 && cy % 4 < 2 && tx > 12 && tx < 20) return Rgb(0xD9E1D9);
                return Rgb(((cx + cy) % 2 == 0) ? 0x536577 : 0x465665);
            case "energia": return tx % 8 < 2 || ty % 8 < 2 ? Rgb(0x59666B) : Rgb(0x253743);
            case "bodega":
                if (cy % 5 == 0 && ty < 4) return (tx / 4) % 2 == 0 ? Rgb(0xD1A15C) : seam;
                return Rgb(0x625949);
            case "refrigeracion": return tx < 2 || ty < 2 ? accent : Rgb(((cx + cy) % 2 == 0) ? 0x6B8E91 : 0x8AA6A4);
            case "observatorio": return (tx is 2 or 29 && ty is 2 or 29) ? accent : Rgb(0x333E59);
            case "reactor": return tx % 8 < 2 || ty % 8 < 2 ? Rgb(0x6F7277) : Rgb(0x321D21);
            default: return cx % 4 == 0 && tx < 2 ? accent : Rgb(((cx + cy) % 2 == 0) ? 0x4A6576 : 0x3D5263);
        }
    }
}
