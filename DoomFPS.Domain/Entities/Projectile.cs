namespace DoomFPS.Domain.Entities
{
    // Bola de fuego, plasma o cohete. Vive en el mundo hasta chocar o agotarse.
    public sealed class Projectile
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public double DirX { get; }
        public double DirY { get; }
        public double Height { get; private set; } = 0.5;
        public double VerticalSlope { get; }
        public double Speed { get; }
        public int Damage { get; }
        public int SplashDamage { get; }
        public double SplashRadius { get; }
        public bool FromPlayer { get; }
        public ProjectileKind Kind { get; }
        public double Radius { get; }
        public bool Alive { get; private set; } = true;
        public int Age { get; private set; }
        public int MaxAge { get; }

        public Projectile(double x, double y, double dirX, double dirY, double speed,
            int damage, bool fromPlayer, ProjectileKind kind,
            int splashDamage = 0, double splashRadius = 0, double radius = 0.16, int maxAge = 360, double verticalSlope = 0)
        {
            double length = Math.Sqrt(dirX * dirX + dirY * dirY);
            if (!double.IsFinite(length) || length < 1e-9) throw new ArgumentOutOfRangeException(nameof(dirX));
            if (!double.IsFinite(verticalSlope)) throw new ArgumentOutOfRangeException(nameof(verticalSlope));
            X = x; Y = y; DirX = dirX / length; DirY = dirY / length;
            VerticalSlope = verticalSlope;
            Speed = speed; Damage = damage; FromPlayer = fromPlayer; Kind = kind;
            SplashDamage = splashDamage; SplashRadius = splashRadius; Radius = radius; MaxAge = maxAge;
        }

        public void Advance(double distance)
        {
            X += DirX * distance; Y += DirY * distance; Height += VerticalSlope * distance;
        }

        public void Age1() { Age++; if (Age >= MaxAge) Alive = false; }
        public void Kill() => Alive = false;
    }

    // Sangre, chispas y humo. Solo decoran: no aplican daño ni bloquean el paso.
    public sealed class Particle
    {
        public double X, Y, VelX, VelY, Height, VelHeight, Size;
        public int Life, MaxLife, Color;
        public ParticleKind Kind;

        public Particle(ParticleKind kind, double x, double y, double velX, double velY,
            double height, double velHeight, int life, int color, double size)
        {
            Kind = kind; X = x; Y = y; VelX = velX; VelY = velY;
            Height = height; VelHeight = velHeight; Life = life; MaxLife = life; Color = color; Size = size;
        }

        public double Fade => MaxLife <= 0 ? 0 : Life / (double)MaxLife;
    }
}
