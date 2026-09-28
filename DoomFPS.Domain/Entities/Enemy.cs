namespace DoomFPS.Domain.Entities
{
    // Un demonio del sector. Guarda su ficha (vida, daño, alcance) y el estado de
    // su animación. El desplazamiento lo decide GameService, que sí conoce el mapa.
    public class Enemy
    {
        // --- Ficha original: los valores por defecto no cambiaron ---
        public string Type { get; set; } = "Cacodemon";
        public int Health { get; set; } = 100;
        public double X { get; private set; }
        public double Y { get; private set; }
        public bool IsAlive => Health > 0;
        public double Speed { get; init; } = 0.8;
        public int Damage { get; init; } = 9;
        public int AttackCooldown { get; private set; }
        public int HitFlashFrames { get; private set; }

        // --- Ficha ampliada de la campaña ---
        public EnemyKind Kind { get; init; } = EnemyKind.Cacodemon;
        public EnemyBehavior Behavior { get; init; } = EnemyBehavior.Chaser;
        public AttackKind Attack { get; init; } = AttackKind.Melee;
        public int MaxHealth { get; set; }
        public double AttackRange { get; init; } = 0.85;
        public double PreferredRange { get; init; }
        public double AggroRange { get; init; } = 8;
        public int AttackDelayFrames { get; init; } = 60;
        public int WindUpFrames { get; init; }
        public int BurstCount { get; init; } = 1;
        public int ProjectileDamage { get; init; }
        public double ProjectileSpeed { get; init; } = 6;
        public ProjectileKind Projectile { get; init; } = ProjectileKind.Fireball;
        public double Radius { get; init; } = 0.23;
        public double SpriteScale { get; init; } = 0.92;
        public bool Flying { get; init; }
        public double Alpha { get; init; } = 1;
        public int Score { get; init; } = 10;
        public string DisplayName { get; init; } = "";

        // --- Estado vivo, lo mueve la capa de aplicación ---
        public EnemyState State { get; private set; } = EnemyState.Idle;
        public int StateFrames { get; private set; }
        public int AnimTimer { get; private set; }
        public int DeathTimer { get; private set; }
        public bool Alerted { get; set; }
        public int WindUp { get; set; }
        public int BurstLeft { get; set; }
        public int DashFrames { get; set; }
        public int StunFrames { get; set; }
        public int SummonCooldown { get; set; }
        public int BossPhase { get; set; }
        public double DashX { get; set; }
        public double DashY { get; set; }
        public double Phase { get; set; }
        public double Bob { get; set; }
        public double FacingX { get; set; } = 1;
        public double FacingY { get; set; }

        public double HealthFraction => MaxHealth > 0 ? Math.Clamp(Health / (double)MaxHealth, 0, 1) : 1;

        public void SetPosition(double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y;
        }

        public void SetState(EnemyState state)
        {
            if (State == state) return;
            State = state; StateFrames = 0;
        }

        // Un paso de reloj: enfría el ataque, avanza la animación y el destello de daño.
        public void Tick()
        {
            if (AttackCooldown > 0) AttackCooldown--;
            if (HitFlashFrames > 0) HitFlashFrames--;
            if (StunFrames > 0) StunFrames--;
            if (SummonCooldown > 0) SummonCooldown--;
            AnimTimer++; StateFrames++;
            if (!IsAlive) { DeathTimer++; return; }
            if (State == EnemyState.Pain && StateFrames > 10) SetState(EnemyState.Walk);
            if (State == EnemyState.Attack && StateFrames > 12) SetState(EnemyState.Walk);
        }

        // Deja el ataque en espera sin gastarlo: lo usan las embestidas y las
        // andanadas, que se preparan antes de golpear.
        public void ArmCooldown(int frames) => AttackCooldown = Math.Max(0, frames);

        public bool TryAttack(Player player)
        {
            if (!IsAlive || !player.IsAlive || AttackCooldown > 0) return false;
            player.TakeDamage(Damage);
            AttackCooldown = AttackDelayFrames;
            return true;
        }

        // Marca el disparo enemigo como consumido sin aplicar daño directo:
        // el proyectil lo entrega después.
        public bool TryBeginRangedAttack()
        {
            if (!IsAlive || AttackCooldown > 0) return false;
            AttackCooldown = AttackDelayFrames;
            return true;
        }

        public void TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            bool wasAlive = IsAlive;
            Health -= amount;
            if (Health < 0) Health = 0;
            HitFlashFrames = 8;
            Alerted = true;
            if (wasAlive && !IsAlive) { SetState(EnemyState.Dead); DeathTimer = 0; }
            else if (wasAlive && State != EnemyState.WindUp && amount > 0) SetState(EnemyState.Pain);
        }
    }
}
