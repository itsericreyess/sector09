namespace DoomFPS.Domain.Entities;

// Bestiario del sector. Cada especie tiene vida, velocidad, alcance, ataque y
// patrón de movimiento propios. La dificultad escala los números al crearlas.
public static class EnemyCatalog
{
    public static Enemy Create(EnemyKind kind, DifficultySettings settings)
    {
        double hp = settings.EnemyHealth, dmg = settings.EnemyDamage;
        double spd = settings.EnemySpeed, cd = settings.EnemyCooldown;
        int Scale(int value, double factor) => Math.Max(1, (int)Math.Round(value * factor));

        return kind switch
        {
            EnemyKind.Zombi => new Enemy
            {
                Kind = kind, Type = "Zombi", DisplayName = "Zombi de seguridad",
                Health = Scale(32, hp), Damage = Scale(7, dmg), ProjectileDamage = Scale(10, dmg),
                Speed = 0.95 * spd, Behavior = EnemyBehavior.Chaser, Attack = AttackKind.Hitscan,
                AttackRange = 0.9, PreferredRange = 7, AggroRange = 10,
                AttackDelayFrames = Scale(100, cd), WindUpFrames = 22,
                SpriteScale = 0.86, Radius = 0.22, Score = 10
            },
            EnemyKind.Imp => new Enemy
            {
                Kind = kind, Type = "Imp", DisplayName = "Imp",
                Health = Scale(62, hp), Damage = Scale(11, dmg), ProjectileDamage = Scale(15, dmg),
                Speed = 1.05 * spd, Behavior = EnemyBehavior.Zigzag, Attack = AttackKind.Projectile,
                Projectile = ProjectileKind.Fireball, ProjectileSpeed = 7.6,
                AttackRange = 1.15, PreferredRange = 5, AggroRange = 11,
                AttackDelayFrames = Scale(95, cd), WindUpFrames = 20,
                SpriteScale = 0.95, Radius = 0.23, Score = 20
            },
            EnemyKind.Demonio => new Enemy
            {
                Kind = kind, Type = "Demonio", DisplayName = "Demonio rosa",
                Health = Scale(140, hp), Damage = Scale(22, dmg),
                Speed = 1.15 * spd, Behavior = EnemyBehavior.Charger, Attack = AttackKind.Melee,
                AttackRange = 1.05, AggroRange = 11,
                AttackDelayFrames = Scale(85, cd), WindUpFrames = 26,
                SpriteScale = 1.02, Radius = 0.28, Score = 35
            },
            EnemyKind.Espectro => new Enemy
            {
                Kind = kind, Type = "Espectro", DisplayName = "Espectro",
                Health = Scale(95, hp), Damage = Scale(16, dmg),
                Speed = 1.4 * spd, Behavior = EnemyBehavior.Circler, Attack = AttackKind.Melee,
                AttackRange = 1.05, AggroRange = 12,
                AttackDelayFrames = Scale(55, cd),
                SpriteScale = 0.98, Radius = 0.24, Alpha = 0.42, Score = 45
            },
            EnemyKind.AlmaPerdida => new Enemy
            {
                Kind = kind, Type = "Alma Perdida", DisplayName = "Alma perdida",
                Health = Scale(38, hp), Damage = Scale(20, dmg),
                Speed = 1.55 * spd, Behavior = EnemyBehavior.Kamikaze, Attack = AttackKind.Explode,
                AttackRange = 0.75, AggroRange = 14,
                AttackDelayFrames = Scale(115, cd), WindUpFrames = 30,
                SpriteScale = 0.58, Radius = 0.20, Flying = true, Score = 25
            },
            EnemyKind.Cacodemon => new Enemy
            {
                Kind = kind, Type = "Cacodemon", DisplayName = "Cacodemon",
                Health = Scale(185, hp), Damage = Scale(15, dmg), ProjectileDamage = Scale(19, dmg),
                Speed = 0.92 * spd, Behavior = EnemyBehavior.Floater, Attack = AttackKind.Projectile,
                Projectile = ProjectileKind.Plasma, ProjectileSpeed = 8.6,
                AttackRange = 1.2, PreferredRange = 6, AggroRange = 12,
                AttackDelayFrames = Scale(100, cd), WindUpFrames = 24,
                SpriteScale = 1.12, Radius = 0.30, Flying = true, Score = 60
            },
            EnemyKind.CaballeroInfernal => new Enemy
            {
                Kind = kind, Type = "Caballero Infernal", DisplayName = "Caballero infernal",
                Health = Scale(340, hp), Damage = Scale(30, dmg), ProjectileDamage = Scale(22, dmg),
                Speed = 1.0 * spd, Behavior = EnemyBehavior.Sentinel, Attack = AttackKind.Projectile,
                Projectile = ProjectileKind.GreenBolt, ProjectileSpeed = 9.4, BurstCount = 2,
                AttackRange = 3.2, PreferredRange = 6.5, AggroRange = 13,
                AttackDelayFrames = Scale(105, cd), WindUpFrames = 26,
                SpriteScale = 1.24, Radius = 0.31, Score = 110
            },
            EnemyKind.Mancubus => new Enemy
            {
                Kind = kind, Type = "Mancubus", DisplayName = "Mancubus",
                Health = Scale(420, hp), Damage = Scale(26, dmg), ProjectileDamage = Scale(20, dmg),
                Speed = 0.66 * spd, Behavior = EnemyBehavior.Sentinel, Attack = AttackKind.Burst,
                Projectile = ProjectileKind.Fireball, ProjectileSpeed = 8.0, BurstCount = 3,
                AttackRange = 2.6, PreferredRange = 7, AggroRange = 13,
                AttackDelayFrames = Scale(130, cd), WindUpFrames = 30,
                SpriteScale = 1.30, Radius = 0.34, Score = 140
            },
            _ => new Enemy
            {
                Kind = EnemyKind.Ciberdemonio, Type = "Ciberdemonio", DisplayName = "CIBERDEMONIO",
                Health = Scale(2600, hp), Damage = Scale(52, dmg), ProjectileDamage = Scale(42, dmg),
                Speed = 0.9 * spd, Behavior = EnemyBehavior.Boss, Attack = AttackKind.BossVolley,
                Projectile = ProjectileKind.Rocket, ProjectileSpeed = 9.5,
                AttackRange = 2.6, PreferredRange = 6, AggroRange = 40,
                AttackDelayFrames = Scale(150, cd), WindUpFrames = 40,
                SpriteScale = 3.1, Radius = 0.75, Score = 2000
            }
        };
    }

    // Ayuda de la fábrica de niveles: crear y colocar en una sola línea.
    public static Enemy At(EnemyKind kind, DifficultySettings settings, double x, double y)
    {
        var enemy = Create(kind, settings);
        enemy.SetPosition(x, y);
        enemy.MaxHealth = enemy.Health;
        return enemy;
    }
}
