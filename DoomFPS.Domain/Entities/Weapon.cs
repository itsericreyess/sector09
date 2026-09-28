namespace DoomFPS.Domain.Entities
{
    // Arma del jugador. Los valores por defecto son los de la escopeta original
    // para que las pruebas y el código heredado sigan comportándose igual.
    public class Weapon
    {
        public string Name { get; set; } = "Escopeta";
        public int Damage { get; set; } = 30;
        public int CooldownFrames { get; init; } = 25;
        public double Range { get; init; } = 12;

        public WeaponKind Kind { get; init; } = WeaponKind.Escopeta;
        public AmmoKind Ammo { get; init; } = AmmoKind.Shells;
        public int Slot { get; init; } = 2;
        public int Pellets { get; init; } = 1;
        public double Spread { get; init; }
        public int AmmoPerShot { get; init; } = 1;
        public bool FiresProjectile { get; init; }
        public ProjectileKind Projectile { get; init; } = ProjectileKind.Plasma;
        public double ProjectileSpeed { get; init; } = 14;
        public int SplashDamage { get; init; }
        public double SplashRadius { get; init; }
        public double Kick { get; init; } = 1;
        public string ShotSound { get; init; } = "shot";
        public string ShortName { get; init; } = "ESCOPETA";

        // Sistema de cargador y reserva. MagazineSize <= 0 significa "sin cargador":
        // el arma consume munición directamente de la reserva, disparo a disparo,
        // como en el proyecto original (así el arma por defecto usada en las
        // pruebas heredadas no cambia de comportamiento).
        public int MagazineSize { get; init; }
        // Munición actualmente en el cargador. Empieza lleno al crear el arma.
        public int Loaded { get; set; }
        // Duración de la recarga manual (tecla R) en fotogramas a 60 FPS.
        public int ReloadFrames { get; init; } = 60;

        // Catálogo completo de la campaña. Un arma por ranura del 1 al 6.
        public static Weapon Create(WeaponKind kind) => kind switch
        {
            WeaponKind.Pistola => new Weapon
            {
                Name = "Pistola de servicio", ShortName = "PISTOLA", Kind = WeaponKind.Pistola,
                Slot = 1, Damage = 13, CooldownFrames = 13, Range = 22,
                Ammo = AmmoKind.Bullets, Pellets = 1, Spread = 0.010, Kick = 0.7, ShotSound = "pistol",
                MagazineSize = 12, Loaded = 12, ReloadFrames = 55
            },
            WeaponKind.Escopeta => new Weapon
            {
                Name = "Escopeta de combate", ShortName = "ESCOPETA", Kind = WeaponKind.Escopeta,
                Slot = 2, Damage = 8, CooldownFrames = 30, Range = 15,
                Ammo = AmmoKind.Shells, Pellets = 8, Spread = 0.075, Kick = 2.2, ShotSound = "shot",
                MagazineSize = 8, Loaded = 8, ReloadFrames = 70
            },
            WeaponKind.EscopetaDoble => new Weapon
            {
                Name = "Escopeta de dos cañones", ShortName = "RECORTADA", Kind = WeaponKind.EscopetaDoble,
                Slot = 3, Damage = 9, CooldownFrames = 55, Range = 12,
                Ammo = AmmoKind.Shells, Pellets = 16, Spread = 0.145, AmmoPerShot = 2,
                Kick = 4.5, ShotSound = "boom",
                MagazineSize = 2, Loaded = 2, ReloadFrames = 95
            },
            WeaponKind.Ametralladora => new Weapon
            {
                Name = "Ametralladora pesada", ShortName = "AMETRALLADORA", Kind = WeaponKind.Ametralladora,
                Slot = 4, Damage = 8, CooldownFrames = 4, Range = 22,
                Ammo = AmmoKind.Bullets, Pellets = 1, Spread = 0.032, Kick = 0.9, ShotSound = "pistol",
                MagazineSize = 40, Loaded = 40, ReloadFrames = 90
            },
            WeaponKind.Plasma => new Weapon
            {
                Name = "Fusil de plasma", ShortName = "PLASMA", Kind = WeaponKind.Plasma,
                Slot = 5, Damage = 20, CooldownFrames = 6, Range = 26,
                Ammo = AmmoKind.Cells, Pellets = 1, FiresProjectile = true,
                Projectile = ProjectileKind.Plasma, ProjectileSpeed = 17, Kick = 0.8, ShotSound = "plasma",
                MagazineSize = 40, Loaded = 40, ReloadFrames = 75
            },
            _ => new Weapon
            {
                Name = "Lanzacohetes", ShortName = "COHETES", Kind = WeaponKind.Lanzacohetes,
                Slot = 6, Damage = 55, CooldownFrames = 48, Range = 30,
                Ammo = AmmoKind.Rockets, Pellets = 1, FiresProjectile = true,
                Projectile = ProjectileKind.Rocket, ProjectileSpeed = 10,
                SplashDamage = 85, SplashRadius = 3.0, Kick = 3.6, ShotSound = "rocket",
                MagazineSize = 4, Loaded = 4, ReloadFrames = 110
            }
        };
    }
}
