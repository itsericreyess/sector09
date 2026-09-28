using DoomFPS.Domain;

namespace DoomFPS.Presentation;

// Lienzo de píxeles con las primitivas mínimas para dibujar los sprites.
// Todo se genera desde código: no hay imágenes ni recursos externos.
public sealed class Sprite
{
    public int Width { get; }
    public int Height { get; }
    public int[] Pixels { get; }

    public Sprite(int width, int height)
    { Width = width; Height = height; Pixels = new int[width * height]; }

    public void Set(int x, int y, int color)
    { if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = color; }

    public int Get(int x, int y)
        => x >= 0 && y >= 0 && x < Width && y < Height ? Pixels[y * Width + x] : 0;

    public void Rect(int x, int y, int w, int h, int color)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++) Set(i, j, color);
    }

    public void Ellipse(int cx, int cy, int rx, int ry, int color)
    {
        if (rx <= 0 || ry <= 0) return;
        for (int j = cy - ry; j <= cy + ry; j++)
            for (int i = cx - rx; i <= cx + rx; i++)
            {
                double dx = (i - cx) / (double)rx, dy = (j - cy) / (double)ry;
                if (dx * dx + dy * dy <= 1) Set(i, j, color);
            }
    }

    public void Ring(int cx, int cy, int rx, int ry, int color)
    {
        for (int j = cy - ry; j <= cy + ry; j++)
            for (int i = cx - rx; i <= cx + rx; i++)
            {
                double dx = (i - cx) / (double)rx, dy = (j - cy) / (double)ry;
                double d = dx * dx + dy * dy;
                if (d <= 1 && d > 0.45) Set(i, j, color);
            }
    }

    public void Line(int x0, int y0, int x1, int y1, int color, int thickness = 1)
    {
        int steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (int s = 0; s <= steps; s++)
        {
            double t = steps == 0 ? 0 : s / (double)steps;
            int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
            if (thickness <= 1) Set(x, y, color);
            else Rect(x - thickness / 2, y - thickness / 2, thickness, thickness, color);
        }
    }

    // Oscurece o aclara lo ya dibujado dentro de una zona (sombreado de volumen).
    public void Tone(int x, int y, int w, int h, double factor)
    {
        for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
            {
                int c = Get(i, j);
                if (c == 0) continue;
                Set(i, j, Palette.Scale(c, factor));
            }
    }

    public void Mirror(int fromX, int toX, int y0, int y1)
    {
        for (int j = y0; j <= y1; j++)
            for (int i = fromX; i <= toX; i++)
                Set(Width - 1 - i, j, Get(i, j));
    }
}

// Colores y utilidades de mezcla compartidas por sprites y renderizador.
public static class Palette
{
    public static int Rgb(int r, int g, int b) => unchecked((int)0xff000000)
        | (Math.Clamp(r, 0, 255) << 16) | (Math.Clamp(g, 0, 255) << 8) | Math.Clamp(b, 0, 255);

    public static int Scale(int color, double factor) => Rgb(
        (int)(((color >> 16) & 255) * factor),
        (int)(((color >> 8) & 255) * factor),
        (int)((color & 255) * factor));

    public static int Mix(int a, int b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Rgb(
            (int)(((a >> 16) & 255) * (1 - t) + ((b >> 16) & 255) * t),
            (int)(((a >> 8) & 255) * (1 - t) + ((b >> 8) & 255) * t),
            (int)((a & 255) * (1 - t) + (b & 255) * t));
    }
}

// Fábrica de sprites: una silueta distinta por especie y una pose por estado.
public static class SpriteFactory
{
    private static int C(int r, int g, int b) => Palette.Rgb(r, g, b);

    public static Sprite Enemy(EnemyKind kind, EnemyState state, int frame) => kind switch
    {
        EnemyKind.Zombi => Zombi(state, frame),
        EnemyKind.Imp => Imp(state, frame),
        EnemyKind.Demonio => Demonio(state, frame, false),
        EnemyKind.Espectro => Demonio(state, frame, true),
        EnemyKind.AlmaPerdida => AlmaPerdida(state, frame),
        EnemyKind.Cacodemon => Cacodemon(state, frame),
        EnemyKind.CaballeroInfernal => Caballero(state, frame),
        EnemyKind.Mancubus => Mancubus(state, frame),
        _ => Ciberdemonio(state, frame)
    };

    // ------------------------------------------------------------------ ZOMBI
    // Guardia reanimado: uniforme verde oliva, rostro pálido y fusil.
    private static Sprite Zombi(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int cloth = C(74, 86, 58), cloth2 = C(58, 68, 46), skin = C(176, 168, 142);
        int dark = C(38, 44, 34), blood = C(120, 32, 30), metal = C(58, 62, 64);
        bool dead = state == EnemyState.Dead;
        int lift = frame == 1 ? 1 : 0;

        if (dead)
        {
            s.Ellipse(16, 41, 12, 5, cloth2);
            s.Ellipse(9, 41, 4, 3, skin);
            s.Rect(18, 39, 11, 3, metal);
            s.Ellipse(20, 43, 8, 3, blood);
            return s;
        }
        // piernas
        s.Rect(11, 34, 5, 12 - lift * 2, cloth2);
        s.Rect(17, 34, 5, 10 + lift * 2, cloth2);
        s.Rect(10, 44 - lift * 2, 7, 3, dark);
        s.Rect(17, 42 + lift * 2, 7, 3, dark);
        // torso
        s.Rect(10, 18, 13, 17, cloth);
        s.Rect(10, 18, 13, 3, cloth2);
        s.Rect(13, 22, 3, 9, dark);
        s.Rect(11, 30, 11, 3, dark);
        s.Rect(15, 24, 5, 5, blood);
        // cabeza
        s.Rect(12, 8, 9, 10, skin);
        s.Rect(12, 6, 9, 3, cloth2);
        s.Rect(13, 12, 2, 2, C(26, 18, 16));
        s.Rect(18, 12, 2, 2, C(26, 18, 16));
        s.Rect(14, 16, 5, 1, blood);
        // brazos y fusil según el estado
        if (state is EnemyState.WindUp or EnemyState.Attack)
        {
            s.Rect(7, 20, 18, 4, cloth);
            s.Rect(21, 19, 9, 3, metal);
            s.Rect(24, 21, 4, 2, C(94, 74, 48));
            if (state == EnemyState.Attack)
            {
                s.Rect(29, 17, 3, 7, C(255, 226, 122));
                s.Rect(27, 19, 5, 3, C(255, 170, 60));
            }
        }
        else
        {
            s.Rect(7, 20 + lift, 4, 14, cloth);
            s.Rect(22, 20 - lift, 4, 14, cloth);
            s.Rect(6, 33 + lift, 5, 3, skin);
            s.Rect(22, 33 - lift, 5, 3, skin);
            s.Rect(23, 26, 3, 12, metal);
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.5);
        return s;
    }

    // -------------------------------------------------------------------- IMP
    // Diablillo marrón, hombros con púas y bola de fuego en la mano.
    private static Sprite Imp(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int hide = C(120, 72, 44), hide2 = C(94, 55, 34), light = C(158, 102, 58);
        int spike = C(206, 190, 142), eye = C(255, 122, 34), dark = C(48, 30, 22);
        bool dead = state == EnemyState.Dead;
        int lift = frame == 1 ? 2 : 0;

        if (dead)
        {
            s.Ellipse(16, 41, 13, 5, hide2);
            s.Ellipse(10, 40, 5, 4, light);
            s.Rect(6, 38, 3, 8, spike);
            s.Ellipse(21, 44, 9, 3, C(120, 30, 32));
            return s;
        }
        // piernas
        s.Rect(9, 33, 6, 11 - lift, hide2);
        s.Rect(18, 33, 6, 9 + lift, hide2);
        s.Rect(7, 43 - lift, 9, 4, dark);
        s.Rect(17, 41 + lift, 9, 4, dark);
        // torso
        s.Rect(8, 16, 16, 18, hide);
        s.Rect(10, 17, 8, 11, light);
        s.Rect(12, 29, 9, 4, hide2);
        // cabeza
        s.Rect(10, 4, 12, 13, light);
        s.Rect(9, 1, 4, 8, spike);
        s.Rect(20, 1, 4, 8, spike);
        s.Rect(11, 9, 4, 3, eye);
        s.Rect(18, 9, 4, 3, eye);
        s.Rect(13, 14, 7, 2, dark);
        for (int i = 0; i < 4; i++) s.Set(13 + i * 2, 15, C(235, 225, 200));
        // hombros con púas
        s.Rect(5, 15, 4, 4, spike);
        s.Rect(23, 15, 4, 4, spike);

        if (state == EnemyState.WindUp)
        {
            s.Rect(3, 8, 6, 12, hide);
            s.Ellipse(5, 6, 5, 5, C(255, 150, 40));
            s.Ellipse(5, 6, 3, 3, C(255, 236, 160));
            s.Rect(23, 19, 6, 14, hide);
        }
        else if (state == EnemyState.Attack)
        {
            s.Rect(3, 14, 26, 5, hide);
            s.Ellipse(29, 14, 4, 4, C(255, 178, 60));
            s.Ellipse(29, 14, 2, 2, C(255, 248, 200));
        }
        else
        {
            s.Rect(3, 18 + lift, 6, 15, hide);
            s.Rect(23, 18 - lift, 6, 15, hide);
            s.Rect(2, 31 + lift, 7, 4, light);
            s.Rect(23, 31 - lift, 7, 4, light);
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.45);
        return s;
    }

    // ---------------------------------------------------------- DEMONIO / ESPECTRO
    // Bestia cuadrúpeda de mandíbula enorme. El espectro es el mismo cuerpo
    // en tonos fríos: el renderizador lo dibuja semitransparente.
    private static Sprite Demonio(EnemyState state, int frame, bool spectre)
    {
        var s = new Sprite(32, 48);
        int body = spectre ? C(96, 106, 128) : C(196, 108, 116);
        int body2 = spectre ? C(70, 78, 98) : C(150, 74, 84);
        int horn = spectre ? C(180, 190, 205) : C(226, 210, 178);
        int gum = spectre ? C(60, 70, 92) : C(126, 44, 50);
        int tooth = C(244, 240, 226);
        bool dead = state == EnemyState.Dead;
        int lift = frame == 1 ? 2 : 0;
        bool open = state is EnemyState.Attack or EnemyState.WindUp;
        int crouch = state == EnemyState.WindUp ? 4 : 0;

        if (dead)
        {
            s.Ellipse(16, 42, 14, 5, body2);
            s.Ellipse(9, 41, 6, 4, body);
            s.Rect(4, 38, 4, 5, horn);
            s.Ellipse(20, 45, 10, 3, spectre ? C(70, 84, 110) : C(122, 30, 34));
            return s;
        }
        // patas
        s.Rect(6, 34 + crouch, 6, 11 - lift - crouch, body2);
        s.Rect(20, 34 + crouch, 6, 9 + lift - crouch, body2);
        s.Rect(5, 44 - lift, 8, 3, C(40, 34, 38));
        s.Rect(19, 42 + lift, 8, 3, C(40, 34, 38));
        // cuerpo macizo
        s.Ellipse(16, 25 + crouch, 13, 12, body);
        s.Ellipse(16, 23 + crouch, 10, 8, body2);
        // cabeza fundida al cuerpo
        s.Ellipse(16, 15 + crouch, 11, 8, body);
        s.Rect(5, 6 + crouch, 4, 8, horn);
        s.Rect(23, 6 + crouch, 4, 8, horn);
        s.Rect(10, 11 + crouch, 4, 3, C(255, 214, 90));
        s.Rect(18, 11 + crouch, 4, 3, C(255, 214, 90));
        // mandíbula
        int jaw = open ? 6 : 2;
        s.Rect(7, 17 + crouch, 18, 3 + jaw, gum);
        for (int i = 0; i < 6; i++)
        {
            s.Rect(8 + i * 3, 17 + crouch, 2, 3, tooth);
            s.Rect(8 + i * 3, 17 + crouch + jaw, 2, 3, tooth);
        }
        // brazos
        s.Rect(1, 20 + crouch + lift, 6, 13, body);
        s.Rect(25, 20 + crouch - lift, 6, 13, body);
        if (state == EnemyState.Attack)
        {
            s.Rect(0, 14, 8, 5, body);
            s.Rect(24, 14, 8, 5, body);
            s.Rect(0, 12, 3, 6, horn);
            s.Rect(29, 12, 3, 6, horn);
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.4);
        return s;
    }

    // ----------------------------------------------------------- ALMA PERDIDA
    // Cráneo en llamas: sin cuerpo, solo fuego girando alrededor.
    private static Sprite AlmaPerdida(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int bone = C(232, 226, 204), bone2 = C(184, 176, 152);
        bool dead = state == EnemyState.Dead;
        if (dead)
        {
            s.Ellipse(16, 42, 9, 4, bone2);
            s.Rect(12, 40, 3, 3, C(40, 34, 30));
            s.Rect(18, 40, 3, 3, C(40, 34, 30));
            return s;
        }
        int flare = state == EnemyState.WindUp ? 4 : state == EnemyState.Attack ? 6 : 0;
        // llamas
        for (int i = 0; i < 14; i++)
        {
            double a = i * 0.4487 + frame * 0.5;
            int fx = 16 + (int)(Math.Cos(a) * (11 + flare));
            int fy = 22 + (int)(Math.Sin(a) * (12 + flare));
            s.Ellipse(fx, fy, 4 + flare / 2, 5 + flare / 2, C(214, 92, 24));
        }
        for (int i = 0; i < 10; i++)
        {
            double a = i * 0.628 - frame * 0.4;
            int fx = 16 + (int)(Math.Cos(a) * (8 + flare));
            int fy = 22 + (int)(Math.Sin(a) * (9 + flare));
            s.Ellipse(fx, fy, 4, 4, C(255, 176, 48));
        }
        // cráneo
        s.Ellipse(16, 20, 9, 9, bone);
        s.Rect(10, 27, 12, 6, bone);
        s.Rect(11, 29, 10, 2, bone2);
        int eye = state == EnemyState.Attack ? C(255, 250, 220) : C(255, 96, 24);
        s.Ellipse(12, 19, 3, 4, eye);
        s.Ellipse(20, 19, 3, 4, eye);
        s.Rect(15, 23, 2, 4, bone2);
        for (int i = 0; i < 4; i++) s.Rect(11 + i * 3, 29, 2, 4, bone);
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.35);
        return s;
    }

    // -------------------------------------------------------------- CACODEMON
    // Esfera flotante roja con un ojo enorme y boca de plasma.
    private static Sprite Cacodemon(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int body = C(178, 52, 42), body2 = C(126, 34, 34), horn = C(214, 196, 148);
        bool dead = state == EnemyState.Dead;
        if (dead)
        {
            s.Ellipse(16, 40, 14, 6, body2);
            s.Ellipse(16, 39, 5, 3, C(226, 220, 200));
            s.Ellipse(16, 44, 12, 3, C(110, 26, 30));
            return s;
        }
        int bob = frame == 1 ? 1 : 0;
        s.Ellipse(16, 22 + bob, 15, 16, body);
        s.Ellipse(13, 18 + bob, 10, 10, C(196, 70, 56));
        s.Ellipse(16, 32 + bob, 12, 7, body2);
        // pinchos del lomo
        for (int i = 0; i < 5; i++) s.Rect(4 + i * 6, 6 + bob + (i % 2), 3, 5, horn);
        // ojo
        int pupil = state == EnemyState.WindUp ? C(90, 170, 255) : C(28, 44, 26);
        s.Ellipse(16, 19 + bob, 7, 6, C(226, 214, 168));
        s.Ellipse(16, 19 + bob, 3, 4, pupil);
        // boca
        int open = state is EnemyState.Attack or EnemyState.WindUp ? 7 : 3;
        s.Ellipse(16, 31 + bob, 9, open, C(56, 20, 24));
        for (int i = 0; i < 5; i++)
        {
            s.Rect(9 + i * 3, 31 + bob - open, 2, 3, C(240, 236, 220));
            s.Rect(9 + i * 3, 31 + bob + open - 3, 2, 3, C(240, 236, 220));
        }
        if (state == EnemyState.Attack)
        {
            s.Ellipse(16, 31 + bob, 5, 5, C(120, 210, 255));
            s.Ellipse(16, 31 + bob, 3, 3, C(232, 250, 255));
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.4);
        return s;
    }

    // ------------------------------------------------------ CABALLERO INFERNAL
    // Coloso con cuernos y fuego verde en los puños.
    private static Sprite Caballero(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int hide = C(150, 132, 106), hide2 = C(112, 96, 76), dark = C(72, 60, 48);
        int horn = C(226, 214, 182), glow = C(96, 226, 74);
        bool dead = state == EnemyState.Dead;
        if (dead)
        {
            s.Ellipse(16, 41, 14, 5, hide2);
            s.Ellipse(8, 40, 5, 4, hide);
            s.Rect(3, 36, 4, 6, horn);
            s.Ellipse(21, 45, 10, 3, C(52, 128, 44));
            return s;
        }
        int lift = frame == 1 ? 2 : 0;
        // piernas de cabra
        s.Rect(8, 32, 7, 10 - lift, hide2);
        s.Rect(18, 32, 7, 8 + lift, hide2);
        s.Rect(6, 41 - lift, 10, 5, dark);
        s.Rect(17, 39 + lift, 10, 5, dark);
        // torso ancho
        s.Rect(7, 14, 18, 19, hide);
        s.Rect(9, 15, 7, 12, C(170, 152, 124));
        s.Rect(11, 28, 11, 4, hide2);
        s.Rect(7, 14, 18, 3, hide2);
        // cabeza
        s.Rect(11, 4, 11, 11, hide);
        s.Rect(6, 1, 5, 7, horn);
        s.Rect(21, 1, 5, 7, horn);
        s.Rect(12, 8, 3, 3, C(240, 96, 40));
        s.Rect(18, 8, 3, 3, C(240, 96, 40));
        s.Rect(13, 12, 7, 2, C(40, 30, 26));
        if (state == EnemyState.WindUp)
        {
            s.Rect(2, 8, 7, 14, hide);
            s.Rect(23, 8, 7, 14, hide);
            s.Ellipse(4, 6, 5, 5, glow);
            s.Ellipse(27, 6, 5, 5, glow);
            s.Ellipse(4, 6, 2, 2, C(224, 255, 214));
            s.Ellipse(27, 6, 2, 2, C(224, 255, 214));
        }
        else if (state == EnemyState.Attack)
        {
            s.Rect(1, 13, 30, 6, hide);
            s.Ellipse(30, 15, 5, 5, glow);
            s.Ellipse(1, 15, 5, 5, glow);
        }
        else
        {
            s.Rect(2, 16 + lift, 7, 16, hide);
            s.Rect(23, 16 - lift, 7, 16, hide);
            s.Rect(1, 30 + lift, 8, 5, hide2);
            s.Rect(23, 30 - lift, 8, 5, hide2);
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.4);
        return s;
    }

    // -------------------------------------------------------------- MANCUBUS
    // Mole obesa con dos cañones por brazos.
    private static Sprite Mancubus(EnemyState state, int frame)
    {
        var s = new Sprite(32, 48);
        int flesh = C(196, 156, 120), flesh2 = C(158, 118, 88), dark = C(94, 68, 52);
        int metal = C(84, 84, 88), metal2 = C(56, 56, 60);
        bool dead = state == EnemyState.Dead;
        if (dead)
        {
            s.Ellipse(16, 41, 15, 6, flesh2);
            s.Rect(3, 38, 7, 4, metal);
            s.Rect(22, 38, 7, 4, metal);
            s.Ellipse(16, 45, 13, 3, C(122, 34, 34));
            return s;
        }
        int lift = frame == 1 ? 1 : 0;
        // piernas cortas
        s.Rect(9, 36, 6, 9 - lift, flesh2);
        s.Rect(18, 36, 6, 8 + lift, flesh2);
        s.Rect(8, 44 - lift, 8, 3, dark);
        s.Rect(17, 43 + lift, 8, 3, dark);
        // barriga
        s.Ellipse(16, 27, 14, 12, flesh);
        s.Ellipse(16, 30, 10, 8, C(214, 178, 142));
        for (int i = 0; i < 4; i++) s.Rect(8, 24 + i * 4, 16, 1, flesh2);
        // torso y cabeza hundida
        s.Rect(9, 12, 14, 8, flesh);
        s.Ellipse(16, 11, 6, 6, C(206, 170, 134));
        s.Rect(12, 9, 3, 3, C(60, 40, 34));
        s.Rect(18, 9, 3, 3, C(60, 40, 34));
        s.Rect(13, 14, 7, 3, C(72, 44, 40));
        // cañones
        int reach = state is EnemyState.Attack or EnemyState.WindUp ? 3 : 0;
        s.Rect(1, 16, 8, 8, metal);
        s.Rect(23, 16, 8, 8, metal);
        s.Rect(0, 18 - reach, 5, 5, metal2);
        s.Rect(27, 18 - reach, 5, 5, metal2);
        if (state == EnemyState.WindUp)
        {
            s.Ellipse(2, 20 - reach, 3, 3, C(255, 150, 60));
            s.Ellipse(29, 20 - reach, 3, 3, C(255, 150, 60));
        }
        if (state == EnemyState.Attack)
        {
            s.Ellipse(2, 20 - reach, 5, 5, C(255, 206, 120));
            s.Ellipse(29, 20 - reach, 5, 5, C(255, 206, 120));
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 32, 48, 1.35);
        return s;
    }

    // ----------------------------------------------------------- CIBERDEMONIO
    // El jefe: el doble de alto, pata mecánica y lanzacohetes por brazo.
    private static Sprite Ciberdemonio(EnemyState state, int frame)
    {
        var s = new Sprite(64, 88);
        int hide = C(142, 116, 92), hide2 = C(104, 84, 66), dark = C(58, 48, 40);
        int metal = C(122, 126, 132), metal2 = C(74, 78, 84), horn = C(236, 226, 196);
        bool dead = state == EnemyState.Dead;
        if (dead)
        {
            s.Ellipse(32, 78, 28, 9, hide2);
            s.Ellipse(16, 74, 11, 8, hide);
            s.Rect(6, 66, 6, 12, horn);
            s.Rect(40, 72, 20, 7, metal2);
            s.Ellipse(38, 84, 22, 4, C(118, 30, 32));
            return s;
        }
        int lift = frame == 1 ? 3 : 0;
        // pata de carne y pata mecánica
        s.Rect(18, 58, 12, 22 - lift, hide2);
        s.Rect(16, 78 - lift, 17, 7, dark);
        s.Rect(36, 58, 9, 20 + lift, metal2);
        s.Rect(34, 76 + lift, 15, 8, metal);
        s.Rect(36, 62, 9, 3, metal);
        // torso
        s.Rect(14, 26, 34, 34, hide);
        s.Rect(18, 28, 14, 22, C(164, 136, 108));
        s.Rect(14, 26, 34, 5, hide2);
        s.Rect(20, 52, 22, 7, hide2);
        for (int i = 0; i < 5; i++) s.Rect(20, 32 + i * 4, 22, 1, hide2);
        // cabeza con cuernos
        s.Rect(21, 8, 20, 19, hide);
        s.Ellipse(31, 16, 11, 10, C(158, 130, 102));
        s.Rect(8, 1, 9, 14, horn);
        s.Rect(45, 1, 9, 14, horn);
        s.Rect(14, 12, 8, 5, horn);
        s.Rect(40, 12, 8, 5, horn);
        int eye = state == EnemyState.WindUp ? C(255, 240, 180) : C(255, 90, 32);
        s.Rect(24, 15, 6, 4, eye);
        s.Rect(34, 15, 6, 4, eye);
        s.Rect(26, 23, 12, 4, C(46, 30, 26));
        for (int i = 0; i < 5; i++) s.Rect(26 + i * 3, 23, 2, 4, C(238, 232, 214));
        // brazo de carne
        s.Rect(4, 30 + lift, 11, 26, hide);
        s.Rect(2, 52 + lift, 14, 8, hide2);
        // lanzacohetes
        s.Rect(46, 28, 16, 14, metal2);
        s.Rect(50, 30, 12, 4, metal);
        s.Rect(58, 32, 6, 8, metal);
        s.Rect(48, 42, 10, 10, metal2);
        if (state == EnemyState.WindUp)
        {
            s.Ellipse(62, 36, 4, 4, C(255, 160, 60));
        }
        if (state == EnemyState.Attack)
        {
            s.Ellipse(62, 36, 8, 7, C(255, 220, 130));
            s.Ellipse(60, 36, 5, 10, C(255, 168, 60));
        }
        if (state == EnemyState.Pain) s.Tone(0, 0, 64, 88, 1.3);
        return s;
    }

    // ------------------------------------------------------------------ OBJETOS
    public static Sprite Pickup(PickupKind kind)
    {
        var s = new Sprite(32, 48);
        switch (kind)
        {
            case PickupKind.RedKey:
            case PickupKind.BlueKey:
            case PickupKind.YellowKey:
                int key = kind == PickupKind.RedKey ? C(228, 62, 52)
                    : kind == PickupKind.BlueKey ? C(64, 150, 244) : C(238, 200, 62);
                s.Rect(5, 17, 23, 19, C(34, 38, 42));
                s.Rect(7, 19, 19, 15, key);
                s.Rect(9, 22, 6, 6, C(232, 216, 152));
                s.Rect(17, 29, 8, 2, C(226, 232, 224));
                s.Rect(7, 19, 19, 2, Palette.Scale(key, 1.35));
                break;
            case PickupKind.Health:
                s.Rect(4, 17, 24, 21, C(196, 202, 182));
                s.Rect(10, 13, 12, 5, C(96, 106, 100));
                s.Rect(13, 20, 6, 15, C(198, 52, 44));
                s.Rect(8, 25, 16, 5, C(198, 52, 44));
                break;
            case PickupKind.MegaHealth:
                s.Ellipse(16, 26, 12, 12, C(70, 190, 236));
                s.Ellipse(13, 22, 6, 6, C(206, 244, 255));
                s.Ring(16, 26, 12, 12, C(226, 250, 255));
                s.Rect(13, 22, 6, 12, C(255, 255, 255));
                s.Rect(10, 25, 12, 6, C(255, 255, 255));
                break;
            case PickupKind.Armor:
                s.Rect(7, 15, 18, 14, C(46, 148, 92));
                s.Rect(9, 29, 14, 8, C(46, 148, 92));
                s.Rect(13, 37, 6, 4, C(36, 116, 74));
                s.Rect(9, 17, 6, 8, C(126, 226, 168));
                s.Rect(7, 15, 18, 2, C(180, 244, 208));
                break;
            case PickupKind.Backpack:
                s.Rect(6, 16, 20, 20, C(96, 74, 44));
                s.Rect(8, 18, 16, 5, C(134, 104, 62));
                s.Rect(10, 26, 5, 8, C(60, 46, 30));
                s.Rect(18, 26, 5, 8, C(60, 46, 30));
                s.Rect(6, 14, 20, 3, C(52, 40, 26));
                break;
            case PickupKind.Bullets:
                s.Rect(6, 24, 20, 12, C(78, 80, 84));
                s.Rect(6, 22, 20, 3, C(126, 128, 132));
                for (int i = 0; i < 5; i++) s.Rect(7 + i * 4, 18, 3, 6, C(212, 176, 72));
                break;
            case PickupKind.Cells:
                s.Rect(8, 16, 16, 22, C(38, 52, 64));
                s.Rect(10, 19, 12, 16, C(72, 198, 244));
                s.Rect(12, 21, 3, 12, C(206, 246, 255));
                s.Rect(8, 14, 16, 3, C(120, 128, 136));
                break;
            case PickupKind.Rockets:
                for (int i = 0; i < 2; i++)
                {
                    int x = 8 + i * 10;
                    s.Rect(x, 20, 7, 16, C(96, 100, 104));
                    s.Rect(x, 15, 7, 6, C(190, 62, 44));
                    s.Rect(x + 1, 34, 5, 4, C(60, 62, 66));
                }
                break;
            case PickupKind.WeaponShotgun:
                s.Rect(3, 24, 26, 5, C(46, 50, 52));
                s.Rect(5, 29, 13, 6, C(112, 74, 44));
                s.Rect(20, 22, 9, 3, C(150, 154, 156));
                s.Rect(11, 28, 4, 6, C(70, 74, 76));
                break;
            case PickupKind.WeaponDoubleShotgun:
                s.Rect(3, 21, 26, 5, C(52, 56, 58));
                s.Rect(3, 27, 26, 5, C(52, 56, 58));
                s.Rect(5, 32, 12, 6, C(126, 84, 48));
                s.Rect(24, 19, 5, 15, C(158, 162, 164));
                break;
            case PickupKind.WeaponMachinegun:
                s.Rect(2, 22, 28, 7, C(58, 62, 64));
                s.Rect(6, 29, 8, 9, C(44, 46, 48));
                s.Rect(24, 20, 6, 11, C(150, 154, 158));
                s.Rect(15, 30, 10, 4, C(90, 94, 98));
                break;
            case PickupKind.WeaponPlasma:
                s.Rect(3, 20, 26, 10, C(48, 60, 72));
                s.Rect(6, 22, 18, 6, C(72, 198, 244));
                s.Rect(26, 22, 5, 6, C(206, 246, 255));
                s.Rect(8, 30, 9, 8, C(38, 46, 56));
                break;
            case PickupKind.WeaponRocket:
                s.Rect(2, 20, 28, 11, C(72, 76, 78));
                s.Rect(4, 22, 10, 7, C(190, 62, 44));
                s.Rect(26, 18, 5, 15, C(46, 48, 50));
                s.Rect(9, 31, 9, 8, C(52, 54, 56));
                break;
            default: // cartuchos
                s.Rect(3, 22, 26, 16, C(124, 100, 46));
                s.Rect(5, 23, 22, 3, C(216, 186, 80));
                for (int i = 0; i < 4; i++)
                {
                    s.Rect(6 + i * 5, 13, 3, 11, C(200, 68, 38));
                    s.Rect(6 + i * 5, 21, 3, 4, C(234, 190, 76));
                }
                break;
        }
        return s;
    }

    // Proyectiles: pequeñas esferas con núcleo brillante.
    public static Sprite Projectile(ProjectileKind kind)
    {
        var s = new Sprite(24, 24);
        (int outer, int inner) = kind switch
        {
            ProjectileKind.Plasma => (C(48, 150, 236), C(214, 246, 255)),
            ProjectileKind.GreenBolt => (C(64, 190, 60), C(216, 255, 200)),
            ProjectileKind.Rocket => (C(120, 122, 126), C(255, 190, 90)),
            ProjectileKind.Soul => (C(226, 158, 60), C(255, 244, 206)),
            _ => (C(226, 108, 26), C(255, 232, 158))
        };
        if (kind == ProjectileKind.Rocket)
        {
            s.Rect(6, 9, 13, 6, outer);
            s.Rect(17, 10, 5, 4, C(198, 74, 46));
            s.Rect(2, 8, 5, 8, inner);
            s.Rect(0, 10, 3, 4, C(255, 236, 190));
        }
        else
        {
            s.Ellipse(12, 12, 9, 9, Palette.Scale(outer, 0.7));
            s.Ellipse(12, 12, 7, 7, outer);
            s.Ellipse(11, 11, 4, 4, inner);
        }
        return s;
    }
}
